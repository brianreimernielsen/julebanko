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

    public async Task<(bool Ok, string? Error, SeasonBackup? Backup)> ResetSeasonAsync(int adminId, string note)
    {
        var season = await seasons.GetActiveSeasonTrackedAsync();
        var numbers = await db.DrawnNumbers.Where(n => n.SeasonId == season.Id).ToListAsync();
        var claims = await db.Claims.Where(c => c.SeasonId == season.Id).ToListAsync();

        var payload = new SeasonSnapshot
        {
            SeasonId = season.Id,
            Year = season.Year,
            Title = season.Title,
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
                ReviewedByAdminId = c.ReviewedByAdminId
            }).ToList()
        };

        var backupRoot = Path.Combine(env.ContentRootPath, "App_Data", "backups");
        Directory.CreateDirectory(backupRoot);
        var fileName = $"season-{season.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json";
        var physical = Path.Combine(backupRoot, fileName);
        await File.WriteAllTextAsync(physical, JsonSerializer.Serialize(payload, JsonOptions));

        var backup = new SeasonBackup
        {
            SeasonId = season.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId,
            FileName = fileName,
            RelativePath = $"/App_Data/backups/{fileName}",
            Note = string.IsNullOrWhiteSpace(note) ? "Season reset" : note.Trim()
        };
        db.SeasonBackups.Add(backup);

        db.DrawnNumbers.RemoveRange(numbers);
        db.Claims.RemoveRange(claims);
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

        var existingNumbers = await db.DrawnNumbers.Where(n => n.SeasonId == season.Id).ToListAsync();
        var existingClaims = await db.Claims.Where(c => c.SeasonId == season.Id).ToListAsync();
        db.DrawnNumbers.RemoveRange(existingNumbers);
        db.Claims.RemoveRange(existingClaims);

        foreach (var n in snapshot.DrawnNumbers)
        {
            db.DrawnNumbers.Add(new DrawnNumber
            {
                SeasonId = season.Id,
                Number = n.Number,
                DrawnAt = n.DrawnAt,
                DrawBatchId = n.DrawBatchId
            });
        }

        foreach (var c in snapshot.Claims)
        {
            db.Claims.Add(new Claim
            {
                SeasonId = season.Id,
                PlayerName = c.PlayerName,
                WinType = c.WinType,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                ReviewedAt = c.ReviewedAt,
                ReviewedByAdminId = c.ReviewedByAdminId
            });
        }

        backup.IsRestored = true;
        await db.SaveChangesAsync();
        return (true, null);
    }

    private sealed class SeasonSnapshot
    {
        public int SeasonId { get; set; }
        public int Year { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<DrawnNumberSnapshot> DrawnNumbers { get; set; } = new();
        public List<ClaimSnapshot> Claims { get; set; } = new();
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
    }
}
