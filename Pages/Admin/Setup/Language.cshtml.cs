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

    [BindProperty]
    public string BrandFont { get; set; } = FontCatalog.DefaultBrandFont;

    [BindProperty]
    public string BodyFont { get; set; } = FontCatalog.DefaultBodyFont;

    public bool Saved { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        DefaultCulture = CultureSupport.Normalize(DefaultCulture);
        // Empty brand names are intentional (logo-only hero).
        BrandNameDa = NormalizeBrand(BrandNameDa);
        BrandNameEn = NormalizeBrand(BrandNameEn);
        BrandNameSv = NormalizeBrand(BrandNameSv);
        BrandFont = FontCatalog.ResolveBrand(BrandFont).Id;
        BodyFont = FontCatalog.ResolveBody(BodyFont).Id;

        await settings.SetManyAsync(new Dictionary<string, string>
        {
            [SettingKeys.DefaultCulture] = DefaultCulture,
            [SettingKeys.BrandNameDa] = BrandNameDa,
            [SettingKeys.BrandNameEn] = BrandNameEn,
            [SettingKeys.BrandNameSv] = BrandNameSv,
            [SettingKeys.BrandFont] = BrandFont,
            [SettingKeys.BodyFont] = BodyFont
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
        BrandFont = FontCatalog.ResolveBrand(
            await settings.GetAsync(SettingKeys.BrandFont, FontCatalog.DefaultBrandFont)).Id;
        BodyFont = FontCatalog.ResolveBody(
            await settings.GetAsync(SettingKeys.BodyFont, FontCatalog.DefaultBodyFont)).Id;
    }

    private static string NormalizeBrand(string? value) => value?.Trim() ?? "";
}
