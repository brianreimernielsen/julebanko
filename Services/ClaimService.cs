using System.Globalization;
using Julebanko.Data;
using Julebanko.Models;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Services;

public class ClaimService(
    AppDbContext db,
    SeasonService seasons,
    EmailService email,
    BingoCardService bingoCards)
{
    public async Task<IReadOnlyList<Claim>> GetApprovedAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        return await db.Claims.AsNoTracking()
            .Where(c => c.SeasonId == season.Id && c.Status == ClaimStatus.Approved)
            .OrderBy(c => c.ReviewedAt ?? c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Claim>> GetPendingAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        return await db.Claims.AsNoTracking()
            .Include(c => c.BingoCard)
            .Where(c => c.SeasonId == season.Id && c.Status == ClaimStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Public player board: pending + approved claims only (rejected never listed).
    /// Approved first, then pending by submit time.
    /// </summary>
    public async Task<IReadOnlyList<Claim>> GetPublicBoardAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        var claims = await db.Claims.AsNoTracking()
            .Where(c => c.SeasonId == season.Id
                        && (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.Approved))
            .ToListAsync();

        return claims
            .OrderBy(c => c.Status == ClaimStatus.Approved ? 0 : 1)
            .ThenBy(c => c.Status == ClaimStatus.Approved
                ? (c.ReviewedAt ?? c.CreatedAt)
                : c.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<Claim>> GetAllForSeasonAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        return await db.Claims.AsNoTracking()
            .Where(c => c.SeasonId == season.Id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Claims (any status) that reference the given series number.</summary>
    public async Task<IReadOnlyList<Claim>> GetBySeriesAsync(string? seriesInput)
    {
        if (!BingoPlateGenerator.TryParseSeries(seriesInput, out var series))
        {
            return Array.Empty<Claim>();
        }

        var season = await seasons.GetActiveSeasonAsync();
        var display = BingoPlateGenerator.FormatSeries(series);
        // Also match raw digit strings players may have typed without zero-padding.
        var raw = series.ToString(CultureInfo.InvariantCulture);
        return await db.Claims.AsNoTracking()
            .Where(c => c.SeasonId == season.Id
                        && c.SeriesNumber != null
                        && (c.SeriesNumber == display || c.SeriesNumber == raw))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Error, ClaimValidationStatus? Validation, bool? ForceAllowed)>
        SubmitAsync(string playerName, WinType winType, string? seriesNumber, bool forceDespiteMismatch = false)
    {
        playerName = (playerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 120)
        {
            return (false, "NameRequired", null, null);
        }

        if (!Enum.IsDefined(winType))
        {
            return (false, "InvalidWinType", null, null);
        }

        if (!BingoPlateGenerator.TryParseSeries(seriesNumber, out var seriesParsed))
        {
            return (false, "SeriesRequired", null, null);
        }

        var seriesDisplay = BingoPlateGenerator.FormatSeries(seriesParsed);

        if (await bingoCards.IsPrizeOncePerTypeAsync())
        {
            if (await bingoCards.PlayerAlreadyHasWinTypeAsync(playerName, winType))
            {
                return (false, "PrizeOnceUsed", null, null);
            }
        }

        var autoValidate = await bingoCards.IsAutoValidateEnabledAsync();
        ClaimValidationStatus validationStatus = ClaimValidationStatus.NotChecked;
        var autoValidated = false;
        string? validationNote = null;
        int? bingoCardId = null;

        if (autoValidate)
        {
            var result = await bingoCards.ValidateClaimAsync(seriesDisplay, winType);
            validationStatus = result.Status;
            autoValidated = result.AutoValidated;
            validationNote = result.Note;
            bingoCardId = result.CardId;

            if (validationStatus != ClaimValidationStatus.Valid && !forceDespiteMismatch)
            {
                return (false, validationStatus == ClaimValidationStatus.CardNotFound
                        ? "SeriesNotFound"
                        : "ValidationMismatch",
                    validationStatus,
                    true);
            }
        }
        else
        {
            // Still resolve card id when possible for admin lookup.
            var card = await bingoCards.FindBySeriesAsync(seriesParsed);
            bingoCardId = card?.Id;
            validationStatus = ClaimValidationStatus.NotChecked;
            validationNote = "AutoValidateOff";
        }

        var season = await seasons.GetActiveSeasonTrackedAsync();
        var claim = new Claim
        {
            SeasonId = season.Id,
            PlayerName = playerName,
            WinType = winType,
            Status = ClaimStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            SeriesNumber = seriesDisplay,
            BingoCardId = bingoCardId,
            ValidationStatus = validationStatus,
            AutoValidated = autoValidated,
            ValidationNote = validationNote
        };
        db.Claims.Add(claim);
        await db.SaveChangesAsync();

        try
        {
            await email.SendClaimNotificationAsync(claim);
        }
        catch
        {
            // Claim is saved; mail failure should not roll back the player claim.
        }

        return (true, null, validationStatus, null);
    }

    public async Task<(bool Ok, string? Error)> ReviewAsync(int claimId, bool approve, int adminId)
    {
        var claim = await db.Claims.FirstOrDefaultAsync(c => c.Id == claimId);
        if (claim is null)
        {
            return (false, "NotFound");
        }

        if (claim.Status != ClaimStatus.Pending)
        {
            return (false, "AlreadyReviewed");
        }

        claim.Status = approve ? ClaimStatus.Approved : ClaimStatus.Rejected;
        claim.ReviewedAt = DateTime.UtcNow;
        claim.ReviewedByAdminId = adminId;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateApprovedAsync(int claimId, string playerName, WinType winType)
    {
        var claim = await db.Claims.FirstOrDefaultAsync(c => c.Id == claimId);
        if (claim is null || claim.Status != ClaimStatus.Approved)
        {
            return (false, "NotFound");
        }

        playerName = (playerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return (false, "NameRequired");
        }

        claim.PlayerName = playerName;
        claim.WinType = winType;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteApprovedAsync(int claimId)
    {
        var claim = await db.Claims.FirstOrDefaultAsync(c => c.Id == claimId);
        if (claim is null || claim.Status != ClaimStatus.Approved)
        {
            return (false, "NotFound");
        }

        db.Claims.Remove(claim);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public static string WinTypeLabel(WinType type, string culture)
    {
        var da = culture.StartsWith("da", StringComparison.OrdinalIgnoreCase);
        var sv = culture.StartsWith("sv", StringComparison.OrdinalIgnoreCase);
        return type switch
        {
            WinType.OneRow => da ? "1 række" : sv ? "1 rad" : "1 row",
            WinType.TwoRows => da ? "2 rækker" : sv ? "2 rader" : "2 rows",
            WinType.FullBoard => da ? "Fuld plade" : sv ? "Full bricka" : "Full board",
            _ => type.ToString()
        };
    }

    public static string ValidationLabel(ClaimValidationStatus status, string culture)
    {
        var da = culture.StartsWith("da", StringComparison.OrdinalIgnoreCase);
        var sv = culture.StartsWith("sv", StringComparison.OrdinalIgnoreCase);
        return status switch
        {
            ClaimValidationStatus.Valid => da ? "Matcher plade" : sv ? "Matchar bricka" : "Matches plate",
            ClaimValidationStatus.Mismatch => da ? "Matcher ikke" : sv ? "Matchar inte" : "Does not match",
            ClaimValidationStatus.CardNotFound => da ? "Plade ikke fundet" : sv ? "Bricka saknas" : "Plate not found",
            _ => da ? "Ikke tjekket" : sv ? "Ej kontrollerad" : "Not checked"
        };
    }
}
