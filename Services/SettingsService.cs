using System.Globalization;
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

    /// <summary>
    /// Brand name for the current UI culture (or an explicit two-letter culture).
    /// Empty string means logo-only hero (no wordmark). Missing keys fall back to defaults.
    /// </summary>
    public async Task<string> GetBrandNameAsync(string? culture = null)
    {
        var lang = CultureSupport.Normalize(
            culture ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var key = CultureSupport.BrandSettingKey(lang);
        var map = await GetAllAsync();
        if (map.TryGetValue(key, out var value))
        {
            return value.Trim();
        }

        return CultureSupport.DefaultBrandName(lang);
    }

    /// <summary>
    /// Brand for titles/email when a blank wordmark is allowed on the player hero.
    /// </summary>
    public async Task<string> GetBrandNameOrFallbackAsync(string? culture = null)
    {
        var name = await GetBrandNameAsync(culture);
        return string.IsNullOrWhiteSpace(name)
            ? CultureSupport.DefaultBrandName(culture)
            : name;
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
