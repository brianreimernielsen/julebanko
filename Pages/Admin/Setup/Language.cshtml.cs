using Julebanko.Data;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin.Setup;

public class LanguageModel(SettingsService settings) : PageModel
{
    [BindProperty]
    public string DefaultCulture { get; set; } = CultureSupport.Danish;

    [BindProperty]
    public string BrandNameDa { get; set; } = CultureSupport.DefaultBrandDa;

    [BindProperty]
    public string BrandNameEn { get; set; } = CultureSupport.DefaultBrandEn;

    [BindProperty]
    public string BrandNameSv { get; set; } = CultureSupport.DefaultBrandSv;

    public bool Saved { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        DefaultCulture = CultureSupport.Normalize(DefaultCulture);
        BrandNameDa = NormalizeBrand(BrandNameDa, CultureSupport.DefaultBrandDa);
        BrandNameEn = NormalizeBrand(BrandNameEn, CultureSupport.DefaultBrandEn);
        BrandNameSv = NormalizeBrand(BrandNameSv, CultureSupport.DefaultBrandSv);

        await settings.SetManyAsync(new Dictionary<string, string>
        {
            [SettingKeys.DefaultCulture] = DefaultCulture,
            [SettingKeys.BrandNameDa] = BrandNameDa,
            [SettingKeys.BrandNameEn] = BrandNameEn,
            [SettingKeys.BrandNameSv] = BrandNameSv
        });

        Saved = true;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        DefaultCulture = CultureSupport.Normalize(
            await settings.GetAsync(SettingKeys.DefaultCulture, CultureSupport.Danish));
        BrandNameDa = await settings.GetAsync(SettingKeys.BrandNameDa, CultureSupport.DefaultBrandDa);
        BrandNameEn = await settings.GetAsync(SettingKeys.BrandNameEn, CultureSupport.DefaultBrandEn);
        BrandNameSv = await settings.GetAsync(SettingKeys.BrandNameSv, CultureSupport.DefaultBrandSv);
    }

    private static string NormalizeBrand(string? value, string fallback)
    {
        var trimmed = value?.Trim() ?? "";
        return string.IsNullOrEmpty(trimmed) ? fallback : trimmed;
    }
}
