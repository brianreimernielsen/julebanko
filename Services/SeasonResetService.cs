using System.Text.Json;
using Julebanko.Data;
using Julebanko.Models;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Services;

public class SeasonResetService(AppDbContext db, SeasonService seasons, IWebHostEnvironment env)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<IReadOnlyList<SeasonBackup>> ListBackupsAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        return await db.SeasonBackups.AsNoTracking()
            .Where(b => b.SeasonId == season.Id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Clears drawn numbers and claims for the active season after backup.
    /// When <paramref name="includePlates"/> is true (default), also backs up and deletes
    /// BingoCardBatches + BingoCards. Undo restores plates when the backup contains them.
    /// </summary>
    public async Task<(bool Ok, string? Error, SeasonBackup? Backup)> ResetSeasonAsync(
        int adminId,
        string note,
        bool includePlates = true)
    {
        var season = await seasons.GetActiveSeasonTrackedAsync();
        var numbers = await db.DrawnNumbers.Where(n => n.SeasonId == season.Id).ToListAsync();
        var claims = await db.Claims.Where(c => c.SeasonId == season.Id).ToListAsync();
        var batches = includePlates
            ? await db.BingoCardBatches.Where(b => b.SeasonId == season.Id).ToListAsync()
            : new List<BingoCardBatch>();
        var cards = includePlates
            ? await db.BingoCards.Where(c => c.SeasonId == season.Id).ToListAsync()
            : new List<BingoCard>();

        var payload = new SeasonSnapshot
        {
            SeasonId = season.Id,
            Year = season.Year,
            Title = season.Title,
            Mode = includePlates ? SnapshotMode.SeasonWithPlates : SnapshotMode.SeasonNoPlates,
            DrawnNumbers = numbers.Select(n => new DrawnNumberSnapshot
            {
                Number = n.Number,
                DrawnAt = n.DrawnAt,
                DrawBatchId = n.DrawBatchId
            }).ToList(),
            Claims = claims.Select(c => new ClaimSnapshot
            {
                PlayerName = c.PlayerName,
                WinType = c.WinType,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                ReviewedAt = c.ReviewedAt,
                ReviewedByAdminId = c.ReviewedByAdminId,
                SeriesNumber = c.SeriesNumber,
                BingoCardId = c.BingoCardId,
                ValidationStatus = c.ValidationStatus,
                AutoValidated = c.AutoValidated,
                ValidationNote = c.ValidationNote
            }).ToList(),
            Batches = batches.Select(ToBatchSnapshot).ToList(),
            Cards = cards.Select(ToCardSnapshot).ToList()
        };

        var noteText = string.IsNullOrWhiteSpace(note)
            ? (includePlates ? "Season reset (incl. plates)" : "Season reset (plates kept)")
            : note.Trim();
        var backup = await WriteBackupAsync(season.Id, adminId, noteText, payload);

        // Claims first (Restrict FK to BingoCard), then plates, then draws.
        db.Claims.RemoveRange(claims);
        if (includePlates)
        {
            db.BingoCards.RemoveRange(cards);
            db.BingoCardBatches.RemoveRange(batches);
        }

        db.DrawnNumbers.RemoveRange(numbers);
        await db.SaveChangesAsync();

        return (true, null, backup);
    }

    /// <summary>
    /// Deletes BingoCardBatches + BingoCards for the active season after a plates-only backup.
    /// Clears BingoCardId on remaining claims (SeriesNumber kept). Drawn numbers and claims stay.
    /// </summary>
    public async Task<(bool Ok, string? Error, SeasonBackup? Backup)> ResetPlatesOnlyAsync(
        int adminId,
        string note)
    {
        var season = await seasons.GetActiveSeasonTrackedAsync();
        var batches = await db.BingoCardBatches.Where(b => b.SeasonId == season.Id).ToListAsync();
        var cards = await db.BingoCards.Where(c => c.SeasonId == season.Id).ToListAsync();

        var payload = new SeasonSnapshot
        {
            SeasonId = season.Id,
            Year = season.Year,
            Title = season.Title,
            Mode = SnapshotMode.PlatesOnly,
            Batches = batches.Select(ToBatchSnapshot).ToList(),
            Cards = cards.Select(ToCardSnapshot).ToList()
        };

        var noteText = string.IsNullOrWhiteSpace(note) ? "Plates reset only" : note.Trim();
        var backup = await WriteBackupAsync(season.Id, adminId, noteText, payload);

        // Detach claims from cards before delete (Restrict FK).
        var linkedClaims = await db.Claims
            .Where(c => c.SeasonId == season.Id && c.BingoCardId != null)
            .ToListAsync();
        foreach (var claim in linkedClaims)
        {
            claim.BingoCardId = null;
        }

        db.BingoCards.RemoveRange(cards);
        db.BingoCardBatches.RemoveRange(batches);
        await db.SaveChangesAsync();

        return (true, null, backup);
    }

    public async Task<(bool Ok, string? Error)> UndoAsync(int backupId)
    {
        var backup = await db.SeasonBackups.FirstOrDefaultAsync(b => b.Id == backupId);
        if (backup is null)
        {
            return (false, "NotFound");
        }

        if (backup.IsRestored)
        {
            return (false, "AlreadyRestored");
        }

        var path = Path.Combine(env.ContentRootPath, "App_Data", "backups", backup.FileName);
        if (!File.Exists(path))
        {
            return (false, "FileMissing");
        }

        var json = await File.ReadAllTextAsync(path);
        var snapshot = JsonSerializer.Deserialize<SeasonSnapshot>(json, JsonOptions);
        if (snapshot is null)
        {
            return (false, "CorruptBackup");
        }

        var season = await seasons.GetActiveSeasonTrackedAsync();
        if (season.Id != snapshot.SeasonId)
        {
            return (false, "SeasonMismatch");
        }

        var mode = snapshot.Mode ?? InferMode(snapshot);

        if (mode == SnapshotMode.PlatesOnly)
        {
            await RestorePlatesOnlyAsync(season.Id, snapshot);
        }
        else
        {
            await RestoreSeasonAsync(season.Id, snapshot, restorePlates: mode == SnapshotMode.SeasonWithPlates
                || snapshot.Batches.Count > 0
                || snapshot.Cards.Count > 0);
        }

        backup.IsRestored = true;
        await db.SaveChangesAsync();
        return (true, null);
    }

    private async Task RestoreSeasonAsync(int seasonId, SeasonSnapshot snapshot, bool restorePlates)
    {
        var existingNumbers = await db.DrawnNumbers.Where(n => n.SeasonId == seasonId).ToListAsync();
        var existingClaims = await db.Claims.Where(c => c.SeasonId == seasonId).ToListAsync();
        db.DrawnNumbers.RemoveRange(existingNumbers);
        db.Claims.RemoveRange(existingClaims);

        Dictionary<int, int>? cardIdMap = null;
        if (restorePlates)
        {
            cardIdMap = await ReplacePlatesAsync(seasonId, snapshot);
        }

        foreach (var n in snapshot.DrawnNumbers)
        {
            db.DrawnNumbers.Add(new DrawnNumber
            {
                SeasonId = seasonId,
                Number = n.Number,
                DrawnAt = n.DrawnAt,
                DrawBatchId = n.DrawBatchId
            });
        }

        foreach (var c in snapshot.Claims)
        {
            int? bingoCardId = null;
            if (c.BingoCardId is int oldCardId && cardIdMap is not null
                && cardIdMap.TryGetValue(oldCardId, out var newCardId))
            {
                bingoCardId = newCardId;
            }
            else if (c.BingoCardId is not null && !restorePlates)
            {
                // Plates were kept; original BingoCardId may still be valid.
                bingoCardId = c.BingoCardId;
            }

            db.Claims.Add(new Claim
            {
                SeasonId = seasonId,
                PlayerName = c.PlayerName,
                WinType = c.WinType,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                ReviewedAt = c.ReviewedAt,
                ReviewedByAdminId = c.ReviewedByAdminId,
                SeriesNumber = c.SeriesNumber,
                BingoCardId = bingoCardId,
                ValidationStatus = c.ValidationStatus,
                AutoValidated = c.AutoValidated,
                ValidationNote = c.ValidationNote
            });
        }
    }

    private async Task RestorePlatesOnlyAsync(int seasonId, SeasonSnapshot snapshot)
    {
        // Clear BingoCardId on claims before replacing plates (Restrict FK).
        var linkedClaims = await db.Claims
            .Where(c => c.SeasonId == seasonId && c.BingoCardId != null)
            .ToListAsync();
        foreach (var claim in linkedClaims)
        {
            claim.BingoCardId = null;
        }

        await ReplacePlatesAsync(seasonId, snapshot);

        // Re-link claims that still have a series number matching a restored plate.
        var claims = await db.Claims.Where(c => c.SeasonId == seasonId).ToListAsync();
        var cardsBySeries = await db.BingoCards
            .Where(c => c.SeasonId == seasonId)
            .ToDictionaryAsync(c => c.SeriesNumber, c => c.Id);

        foreach (var claim in claims)
        {
            if (claim.BingoCardId is not null)
            {
                continue;
            }

            if (BingoPlateGenerator.TryParseSeries(claim.SeriesNumber, out var series)
                && cardsBySeries.TryGetValue(series, out var cardId))
            {
                claim.BingoCardId = cardId;
            }
        }
    }

    /// <summary>Deletes current plates and inserts snapshot batches/cards. Returns old card id → new id.</summary>
    private async Task<Dictionary<int, int>> ReplacePlatesAsync(int seasonId, SeasonSnapshot snapshot)
    {
        var existingCards = await db.BingoCards.Where(c => c.SeasonId == seasonId).ToListAsync();
        var existingBatches = await db.BingoCardBatches.Where(b => b.SeasonId == seasonId).ToListAsync();
        db.BingoCards.RemoveRange(existingCards);
        db.BingoCardBatches.RemoveRange(existingBatches);
        // Flush deletes before inserts so unique (SeasonId, SeriesNumber) indexes stay happy.
        await db.SaveChangesAsync();

        var batchEntities = new List<(int OldId, BingoCardBatch Entity)>();
        foreach (var b in snapshot.Batches.OrderBy(x => x.Id))
        {
            var entity = new BingoCardBatch
            {
                SeasonId = seasonId,
                CreatedAt = b.CreatedAt,
                CreatedByAdminId = b.CreatedByAdminId,
                PlateCount = b.PlateCount,
                PlatesPerPage = b.PlatesPerPage,
                BannerText = b.BannerText,
                Note = b.Note
            };
            db.BingoCardBatches.Add(entity);
            batchEntities.Add((b.Id, entity));
        }

        await db.SaveChangesAsync();
        var batchIdMap = batchEntities.ToDictionary(x => x.OldId, x => x.Entity.Id);

        var cardEntities = new List<(int OldId, BingoCard Entity)>();
        foreach (var c in snapshot.Cards.OrderBy(x => x.Id))
        {
            if (!batchIdMap.TryGetValue(c.BatchId, out var newBatchId))
            {
                continue;
            }

            var entity = new BingoCard
            {
                BatchId = newBatchId,
                SeasonId = seasonId,
                SeriesNumber = c.SeriesNumber,
                CellsJson = c.CellsJson,
                Fingerprint = c.Fingerprint,
                CreatedAt = c.CreatedAt
            };
            db.BingoCards.Add(entity);
            cardEntities.Add((c.Id, entity));
        }

        await db.SaveChangesAsync();
        return cardEntities.ToDictionary(x => x.OldId, x => x.Entity.Id);
    }

    private async Task<SeasonBackup> WriteBackupAsync(
        int seasonId,
        int adminId,
        string note,
        SeasonSnapshot payload)
    {
        var backupRoot = Path.Combine(env.ContentRootPath, "App_Data", "backups");
        Directory.CreateDirectory(backupRoot);
        var fileName = $"season-{seasonId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json";
        var physical = Path.Combine(backupRoot, fileName);
        await File.WriteAllTextAsync(physical, JsonSerializer.Serialize(payload, JsonOptions));

        var backup = new SeasonBackup
        {
            SeasonId = seasonId,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId,
            FileName = fileName,
            RelativePath = $"/App_Data/backups/{fileName}",
            Note = note
        };
        db.SeasonBackups.Add(backup);
        await db.SaveChangesAsync();
        return backup;
    }

    private static BingoCardBatchSnapshot ToBatchSnapshot(BingoCardBatch b) => new()
    {
        Id = b.Id,
        CreatedAt = b.CreatedAt,
        CreatedByAdminId = b.CreatedByAdminId,
        PlateCount = b.PlateCount,
        PlatesPerPage = b.PlatesPerPage,
        BannerText = b.BannerText,
        Note = b.Note
    };

    private static BingoCardSnapshot ToCardSnapshot(BingoCard c) => new()
    {
        Id = c.Id,
        BatchId = c.BatchId,
        SeriesNumber = c.SeriesNumber,
        CellsJson = c.CellsJson,
        Fingerprint = c.Fingerprint,
        CreatedAt = c.CreatedAt
    };

    private static SnapshotMode InferMode(SeasonSnapshot snapshot)
    {
        // Legacy backups (pre-plates) have no Mode and no plate lists.
        if (snapshot.Batches.Count > 0 || snapshot.Cards.Count > 0)
        {
            if (snapshot.DrawnNumbers.Count == 0 && snapshot.Claims.Count == 0)
            {
                return SnapshotMode.PlatesOnly;
            }

            return SnapshotMode.SeasonWithPlates;
        }

        return SnapshotMode.SeasonNoPlates;
    }

    private enum SnapshotMode
    {
        SeasonNoPlates = 0,
        SeasonWithPlates = 1,
        PlatesOnly = 2
    }

    private sealed class SeasonSnapshot
    {
        public int SeasonId { get; set; }
        public int Year { get; set; }
        public string Title { get; set; } = string.Empty;
        public SnapshotMode? Mode { get; set; }
        public List<DrawnNumberSnapshot> DrawnNumbers { get; set; } = new();
        public List<ClaimSnapshot> Claims { get; set; } = new();
        public List<BingoCardBatchSnapshot> Batches { get; set; } = new();
        public List<BingoCardSnapshot> Cards { get; set; } = new();
    }

    private sealed class DrawnNumberSnapshot
    {
        public int Number { get; set; }
        public DateTime DrawnAt { get; set; }
        public Guid DrawBatchId { get; set; }
    }

    private sealed class ClaimSnapshot
    {
        public string PlayerName { get; set; } = string.Empty;
        public WinType WinType { get; set; }
        public ClaimStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByAdminId { get; set; }
        public string? SeriesNumber { get; set; }
        public int? BingoCardId { get; set; }
        public ClaimValidationStatus ValidationStatus { get; set; }
        public bool AutoValidated { get; set; }
        public string? ValidationNote { get; set; }
    }

    private sealed class BingoCardBatchSnapshot
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedByAdminId { get; set; }
        public int PlateCount { get; set; }
        public int PlatesPerPage { get; set; }
        public string? BannerText { get; set; }
        public string? Note { get; set; }
    }

    private sealed class BingoCardSnapshot
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public int SeriesNumber { get; set; }
        public string CellsJson { get; set; } = "[]";
        public string Fingerprint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
