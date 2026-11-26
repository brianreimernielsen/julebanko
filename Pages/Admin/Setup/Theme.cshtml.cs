using Julebanko.Data;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin.Setup;

public class ThemeModel(SettingsService settings) : PageModel
{
    [BindProperty] public string PlayerBgColor { get; set; } = "#8B1E1E";
    [BindProperty] public string AdminBgColor { get; set; } = "#1B4332";
    [BindProperty] public string AccentColor { get; set; } = "#D4A017";
    [BindProperty] public string PlayerTextColor { get; set; } = "#FFF8F0";
    [BindProperty] public string AdminTextColor { get; set; } = "#F0F7F2";
    [BindProperty] public string SeasonTitle { get; set; } = "Julebanko";
    [BindProperty] public bool PlayerSnowEnabled { get; set; } = true;
    [BindProperty] public int PlayerSnowIntensity { get; set; } = 1;
    [BindProperty] public bool PlayerNisseEnabled { get; set; } = true;
    public bool Saved { get; private set; }
    public string? ColorError { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (PlayerSnowIntensity < 1 || PlayerSnowIntensity > 5)
        {
            PlayerSnowIntensity = 1;
        }

        if (!TryNormalizeAll(
                out var playerBg, out var adminBg, out var accent,
                out var playerText, out var adminText))
        {
            ColorError = "Invalid hex color. Use 3 or 6 hex digits (with or without #).";
            await LoadAsync();
            return Page();
        }

        await settings.SetManyAsync(new Dictionary<string, string>
        {
            [SettingKeys.PlayerBgColor] = playerBg,
            [SettingKeys.AdminBgColor] = adminBg,
            [SettingKeys.AccentColor] = accent,
            [SettingKeys.PlayerTextColor] = playerText,
            [SettingKeys.AdminTextColor] = adminText,
            [SettingKeys.SeasonTitle] = SeasonTitle.Trim(),
            [SettingKeys.PlayerSnowEnabled] = PlayerSnowEnabled ? "true" : "false",
            [SettingKeys.PlayerSnowIntensity] = PlayerSnowIntensity.ToString(),
            [SettingKeys.PlayerSnowGroundEnabled] = "true",
            [SettingKeys.PlayerNisseEnabled] = PlayerNisseEnabled ? "true" : "false"
        });
        Saved = true;
        await LoadAsync();
        return Page();
    }

    private bool TryNormalizeAll(
        out string playerBg, out string adminBg, out string accent,
        out string playerText, out string adminText)
    {
        playerBg = adminBg = accent = playerText = adminText = "";
        return ColorHex.TryNormalize(PlayerBgColor, out playerBg)
               && ColorHex.TryNormalize(AdminBgColor, out adminBg)
               && ColorHex.TryNormalize(AccentColor, out accent)
               && ColorHex.TryNormalize(PlayerTextColor, out playerText)
               && ColorHex.TryNormalize(AdminTextColor, out adminText);
    }

    private async Task LoadAsync()
    {
        PlayerBgColor = await settings.GetAsync(SettingKeys.PlayerBgColor, PlayerBgColor);
        AdminBgColor = await settings.GetAsync(SettingKeys.AdminBgColor, AdminBgColor);
        AccentColor = await settings.GetAsync(SettingKeys.AccentColor, AccentColor);
        PlayerTextColor = await settings.GetAsync(SettingKeys.PlayerTextColor, PlayerTextColor);
        AdminTextColor = await settings.GetAsync(SettingKeys.AdminTextColor, AdminTextColor);
        SeasonTitle = await settings.GetAsync(SettingKeys.SeasonTitle, SeasonTitle);
        var snow = await settings.GetAsync(SettingKeys.PlayerSnowEnabled, "true");
        PlayerSnowEnabled = !string.Equals(snow, "false", StringComparison.OrdinalIgnoreCase) && snow != "0";
        var intensity = await settings.GetAsync(SettingKeys.PlayerSnowIntensity, "1");
        if (!int.TryParse(intensity, out var level) || level < 1 || level > 5)
        {
            level = 1;
        }

        PlayerSnowIntensity = level;
        var nisse = await settings.GetAsync(SettingKeys.PlayerNisseEnabled, "true");
        PlayerNisseEnabled = !string.Equals(nisse, "false", StringComparison.OrdinalIgnoreCase) && nisse != "0";
    }
}
