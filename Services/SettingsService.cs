using Julebanko.Data;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Services;

public class SettingsService(AppDbContext db)
{
    private Dictionary<string, string>? _cache;

    public async Task<string> GetAsync(string key, string fallback = "")
    {
        var map = await GetAllAsync();
        return map.TryGetValue(key, out var value) ? value : fallback;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        _cache = await db.AppSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value);
        return _cache;
    }

    public async Task SetAsync(string key, string value)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
        {
            db.AppSettings.Add(new Models.AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync();
        _cache = null;
    }

    public async Task SetManyAsync(IDictionary<string, string> values)
    {
        var keys = values.Keys.ToList();
        var existing = await db.AppSettings.Where(s => keys.Contains(s.Key)).ToListAsync();
        foreach (var (key, value) in values)
        {
            var row = existing.FirstOrDefault(s => s.Key == key);
            if (row is null)
            {
                db.AppSettings.Add(new Models.AppSetting { Key = key, Value = value });
            }
            else
            {
                row.Value = value;
            }
        }

        await db.SaveChangesAsync();
        _cache = null;
    }

    public void InvalidateCache() => _cache = null;
}
