using Julebanko.Data;
using Julebanko.Models;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Services;

public class SeasonService(AppDbContext db)
{
    public async Task<Season> GetActiveSeasonAsync()
    {
        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive);
        if (season is not null)
        {
            return season;
        }

        season = await db.Seasons.OrderByDescending(s => s.Year).FirstOrDefaultAsync();
        if (season is not null)
        {
            return season;
        }

        throw new InvalidOperationException("No season configured. Run database seed.");
    }

    public async Task<Season> GetActiveSeasonTrackedAsync()
    {
        var season = await db.Seasons.FirstOrDefaultAsync(s => s.IsActive)
            ?? await db.Seasons.OrderByDescending(s => s.Year).FirstOrDefaultAsync();
        return season ?? throw new InvalidOperationException("No season configured.");
    }
}
