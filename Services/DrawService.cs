using Julebanko.Data;
using Julebanko.Models;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Services;

public class DrawService(AppDbContext db, SeasonService seasons)
{
    /// <summary>Inclusive banko pool: 1–90 (Scandinavian banko).</summary>
    public const int MinNumber = 1;
    public const int MaxNumber = 90;
    public const int PoolSize = MaxNumber - MinNumber + 1; // 90

    public async Task<IReadOnlyList<DrawnNumber>> GetAllAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        return await db.DrawnNumbers.AsNoTracking()
            .Where(n => n.SeasonId == season.Id)
            .OrderBy(n => n.DrawnAt)
            .ThenBy(n => n.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Numbers drawn on the server's local calendar day.
    /// "Today" uses <see cref="DateTime.Now"/> (OS / IIS timezone), not UTC midnight.
    /// DrawnAt is stored as UTC; Unspecified values from SQL are treated as UTC.
    /// </summary>
    public async Task<IReadOnlyList<DrawnNumber>> GetTodaysAsync(DateTime? nowLocal = null)
    {
        var all = await GetAllAsync();
        var today = (nowLocal ?? DateTime.Now).Date;
        return all.Where(n => ToLocalDrawnDay(n.DrawnAt) == today).ToList();
    }

    private static DateTime ToLocalDrawnDay(DateTime drawnAt)
    {
        var utc = drawnAt.Kind switch
        {
            DateTimeKind.Utc => drawnAt,
            DateTimeKind.Local => drawnAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(drawnAt, DateTimeKind.Utc)
        };
        return utc.ToLocalTime().Date;
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<int> Numbers)> DrawAsync(int count)
    {
        if (count < 1 || count > 20)
        {
            return (false, "Count must be between 1 and 20.", Array.Empty<int>());
        }

        var season = await seasons.GetActiveSeasonTrackedAsync();
        var used = await db.DrawnNumbers
            .Where(n => n.SeasonId == season.Id)
            .Select(n => n.Number)
            .ToListAsync();

        var available = Enumerable.Range(MinNumber, PoolSize).Except(used).ToList();
        if (available.Count < count)
        {
            return (false, $"Only {available.Count} numbers left in the season.", Array.Empty<int>());
        }

        var batchId = Guid.NewGuid();
        var drawnAt = DateTime.UtcNow;
        var rng = Random.Shared;
        var picked = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            var idx = rng.Next(available.Count);
            picked.Add(available[idx]);
            available.RemoveAt(idx);
        }

        foreach (var number in picked)
        {
            db.DrawnNumbers.Add(new DrawnNumber
            {
                SeasonId = season.Id,
                Number = number,
                DrawnAt = drawnAt,
                DrawBatchId = batchId
            });
        }

        await db.SaveChangesAsync();
        return (true, null, picked);
    }
}
