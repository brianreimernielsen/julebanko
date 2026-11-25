using Julebanko.Data;
using Julebanko.Models;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Services;

public class BingoCardService(
    AppDbContext db,
    SeasonService seasons,
    SettingsService settings,
    BingoPdfService pdf,
    DrawService draws)
{
    public async Task<bool> IsAutoValidateEnabledAsync()
    {
        var raw = await settings.GetAsync(SettingKeys.BingoAutoValidate, "true");
        return !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) && raw != "0";
    }

    public async Task<bool> IsPrizeOncePerTypeAsync()
    {
        var raw = await settings.GetAsync(SettingKeys.BingoPrizeOncePerType, "true");
        return !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) && raw != "0";
    }

    /// <summary>
    /// Builds a one-page sample PDF using dummy series numbers.
    /// Does not create BingoCardBatch/BingoCard rows and does not consume series numbers.
    /// </summary>
    public async Task<byte[]> BuildPreviewPdfAsync(
        int platesPerPage,
        string? bannerText,
        BingoPdfStyleOverrides? styleOverrides = null)
    {
        platesPerPage = platesPerPage is 1 or 2 or 3 or 4 or 6 or 8 or 9 ? platesPerPage : 6;

        // Deterministic sample plates so preview is stable across refreshes with the same settings.
        var rng = new Random(20261125);
        var cards = new List<BingoCard>(platesPerPage);
        // High dummy series range — never written to DB; visually distinct from real 000001… series.
        const int previewSeriesBase = 990001;
        for (var i = 0; i < platesPerPage; i++)
        {
            var grid = BingoPlateGenerator.Generate(rng);
            cards.Add(new BingoCard
            {
                SeriesNumber = previewSeriesBase + i,
                CellsJson = BingoPlateGenerator.ToJson(grid),
                Fingerprint = BingoPlateGenerator.Fingerprint(grid),
                CreatedAt = DateTime.UtcNow
            });
        }

        return await pdf.BuildPdfAsync(cards, platesPerPage, bannerText, styleOverrides);
    }

    public async Task<(BingoCardBatch Batch, byte[] Pdf)> GenerateBatchAsync(
        int plateCount,
        int platesPerPage,
        int adminId,
        string? bannerText,
        string? note)
    {
        if (plateCount < 1 || plateCount > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(plateCount), "Plate count must be 1–500.");
        }

        platesPerPage = platesPerPage is 1 or 2 or 3 or 4 or 6 or 8 or 9 ? platesPerPage : 6;

        var season = await seasons.GetActiveSeasonTrackedAsync();
        var nextSeries = await NextSeriesNumberAsync(season.Id);
        var existingFingerprints = await db.BingoCards.AsNoTracking()
            .Where(c => c.SeasonId == season.Id)
            .Select(c => c.Fingerprint)
            .ToListAsync();
        var fingerprints = new HashSet<string>(existingFingerprints, StringComparer.Ordinal);

        var batch = new BingoCardBatch
        {
            SeasonId = season.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = adminId,
            PlateCount = plateCount,
            PlatesPerPage = platesPerPage,
            BannerText = string.IsNullOrWhiteSpace(bannerText) ? null : bannerText.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
        db.BingoCardBatches.Add(batch);

        var rng = Random.Shared;
        var cards = new List<BingoCard>(plateCount);
        for (var i = 0; i < plateCount; i++)
        {
            int?[,] grid = null!;
            string fingerprint;
            var attempts = 0;
            do
            {
                grid = BingoPlateGenerator.Generate(rng);
                fingerprint = BingoPlateGenerator.Fingerprint(grid);
                attempts++;
                if (attempts > 20_000)
                {
                    throw new InvalidOperationException("Could not generate enough unique plates for this season.");
                }
            } while (!fingerprints.Add(fingerprint));

            var card = new BingoCard
            {
                Batch = batch,
                SeasonId = season.Id,
                SeriesNumber = nextSeries++,
                CellsJson = BingoPlateGenerator.ToJson(grid),
                Fingerprint = fingerprint,
                CreatedAt = DateTime.UtcNow
            };
            db.BingoCards.Add(card);
            cards.Add(card);
        }

        await db.SaveChangesAsync();
        var pdfBytes = await pdf.BuildPdfAsync(cards, platesPerPage, batch.BannerText);
        return (batch, pdfBytes);
    }

    public async Task<byte[]?> GetBatchPdfAsync(int batchId)
    {
        var batch = await db.BingoCardBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch is null)
        {
            return null;
        }

        var cards = await db.BingoCards.AsNoTracking()
            .Where(c => c.BatchId == batchId)
            .OrderBy(c => c.SeriesNumber)
            .ToListAsync();
        if (cards.Count == 0)
        {
            return null;
        }

        return await pdf.BuildPdfAsync(cards, batch.PlatesPerPage, batch.BannerText);
    }

    public async Task<IReadOnlyList<BingoCardBatch>> ListBatchesAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        return await db.BingoCardBatches.AsNoTracking()
            .Where(b => b.SeasonId == season.Id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<BingoCardListItem> Items, int Total)> SearchCardsAsync(
        string? seriesQuery,
        int skip,
        int take)
    {
        var season = await seasons.GetActiveSeasonAsync();
        var query = db.BingoCards.AsNoTracking().Where(c => c.SeasonId == season.Id);

        if (BingoPlateGenerator.TryParseSeries(seriesQuery, out var series))
        {
            query = query.Where(c => c.SeriesNumber == series);
        }
        else if (!string.IsNullOrWhiteSpace(seriesQuery))
        {
            // No digits → empty result rather than listing everything unexpectedly.
            return (Array.Empty<BingoCardListItem>(), 0);
        }

        var total = await query.CountAsync();
        var cards = await query
            .OrderBy(c => c.SeriesNumber)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync();

        var drawn = (await draws.GetAllAsync()).Select(d => d.Number).ToHashSet();
        var cardIds = cards.Select(c => c.Id).ToList();
        var claims = await db.Claims.AsNoTracking()
            .Where(c => c.SeasonId == season.Id
                        && c.Status == ClaimStatus.Approved
                        && c.BingoCardId != null
                        && cardIds.Contains(c.BingoCardId.Value))
            .Select(c => new { c.BingoCardId, c.WinType, c.SeriesNumber })
            .ToListAsync();

        var seriesDisplays = cards.Select(c => BingoPlateGenerator.FormatSeries(c.SeriesNumber)).ToList();
        var claimsBySeries = await db.Claims.AsNoTracking()
            .Where(c => c.SeasonId == season.Id
                        && c.Status == ClaimStatus.Approved
                        && c.SeriesNumber != null
                        && seriesDisplays.Contains(c.SeriesNumber))
            .Select(c => new { c.SeriesNumber, c.WinType })
            .ToListAsync();

        var items = cards.Select(card =>
        {
            var grid = BingoPlateGenerator.FromJson(card.CellsJson);
            var hitRows = BingoPlateGenerator.CompletedRowIndexes(grid, drawn);
            var wins = claims.Where(c => c.BingoCardId == card.Id).ToList();
            var seriesFmt = BingoPlateGenerator.FormatSeries(card.SeriesNumber);
            var extra = claimsBySeries.Where(c =>
                    string.Equals(c.SeriesNumber, seriesFmt, StringComparison.Ordinal)
                    || (BingoPlateGenerator.TryParseSeries(c.SeriesNumber, out var sn) && sn == card.SeriesNumber))
                .ToList();
            var winTypes = wins.Select(w => w.WinType)
                .Concat(extra.Select(w => w.WinType))
                .Distinct()
                .ToList();

            return new BingoCardListItem(
                card.Id,
                card.SeriesNumber,
                seriesFmt,
                card.BatchId,
                card.CreatedAt,
                hitRows,
                winTypes.Count > 0,
                winTypes,
                grid);
        }).ToList();

        return (items, total);
    }

    public async Task<BingoCard?> FindBySeriesAsync(int seriesNumber)
    {
        var season = await seasons.GetActiveSeasonAsync();
        return await db.BingoCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SeasonId == season.Id && c.SeriesNumber == seriesNumber);
    }

    public async Task<(ClaimValidationStatus Status, bool AutoValidated, string? Note, int? CardId)>
        ValidateClaimAsync(string? seriesInput, WinType winType)
    {
        if (!BingoPlateGenerator.TryParseSeries(seriesInput, out var series))
        {
            return (ClaimValidationStatus.CardNotFound, false, "InvalidSeries", null);
        }

        var card = await FindBySeriesAsync(series);
        if (card is null)
        {
            return (ClaimValidationStatus.CardNotFound, false, "CardNotFound", null);
        }

        var drawn = (await draws.GetAllAsync()).Select(d => d.Number).ToHashSet();
        var grid = BingoPlateGenerator.FromJson(card.CellsJson);
        var hitRows = BingoPlateGenerator.CompletedRowIndexes(grid, drawn);
        var matches = BingoPlateGenerator.MatchesWinType(grid, drawn, winType);
        var hitNote = hitRows.Count == 0
            ? "NoRowsHit"
            : "RowsHit:" + string.Join(',', hitRows);

        if (matches)
        {
            return (ClaimValidationStatus.Valid, true, hitNote, card.Id);
        }

        return (ClaimValidationStatus.Mismatch, false, hitNote, card.Id);
    }

    public async Task<bool> PlayerAlreadyHasWinTypeAsync(string playerName, WinType winType)
    {
        var season = await seasons.GetActiveSeasonAsync();
        var name = playerName.Trim();
        return await db.Claims.AsNoTracking().AnyAsync(c =>
            c.SeasonId == season.Id
            && c.PlayerName == name
            && c.WinType == winType
            && (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.Approved));
    }

    public async Task<int> CountPlayerDistinctWinTypesAsync(string playerName)
    {
        var season = await seasons.GetActiveSeasonAsync();
        var name = playerName.Trim();
        return await db.Claims.AsNoTracking()
            .Where(c => c.SeasonId == season.Id
                        && c.PlayerName == name
                        && (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.Approved))
            .Select(c => c.WinType)
            .Distinct()
            .CountAsync();
    }

    private async Task<int> NextSeriesNumberAsync(int seasonId)
    {
        var max = await db.BingoCards.AsNoTracking()
            .Where(c => c.SeasonId == seasonId)
            .Select(c => (int?)c.SeriesNumber)
            .MaxAsync();
        return (max ?? 0) + 1;
    }
}

public sealed record BingoCardListItem(
    int Id,
    int SeriesNumber,
    string SeriesDisplay,
    int BatchId,
    DateTime CreatedAt,
    IReadOnlyList<int> HitRows,
    bool HasApprovedWin,
    IReadOnlyList<WinType> ApprovedWinTypes,
    int?[,] Grid);
