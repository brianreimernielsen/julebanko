using Julebanko.Data;
using Microsoft.AspNetCore.Localization;

namespace Julebanko.Services;

/// <summary>
/// Supplies the site default culture from AppSettings when no culture cookie is present.
/// Cookie (language switcher) stays first in the provider list and wins when set.
/// </summary>
public sealed class AppSettingsCultureProvider : RequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        try
        {
            var settings = httpContext.RequestServices.GetRequiredService<SettingsService>();
            var culture = CultureSupport.Normalize(
                await settings.GetAsync(SettingKeys.DefaultCulture, CultureSupport.Danish));
            return new ProviderCultureResult(culture, culture);
        }
        catch
        {
            // DB unavailable during startup/outage — fall through to DefaultRequestCulture.
            return null;
        }
    }
}
