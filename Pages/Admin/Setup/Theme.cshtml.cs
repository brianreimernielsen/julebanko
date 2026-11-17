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
    public bool Saved { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await settings.SetManyAsync(new Dictionary<string, string>
        {
            [SettingKeys.PlayerBgColor] = PlayerBgColor,
            [SettingKeys.AdminBgColor] = AdminBgColor,
            [SettingKeys.AccentColor] = AccentColor,
            [SettingKeys.PlayerTextColor] = PlayerTextColor,
            [SettingKeys.AdminTextColor] = AdminTextColor,
            [SettingKeys.SeasonTitle] = SeasonTitle.Trim()
        });
        Saved = true;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        PlayerBgColor = await settings.GetAsync(SettingKeys.PlayerBgColor, PlayerBgColor);
        AdminBgColor = await settings.GetAsync(SettingKeys.AdminBgColor, AdminBgColor);
        AccentColor = await settings.GetAsync(SettingKeys.AccentColor, AccentColor);
        PlayerTextColor = await settings.GetAsync(SettingKeys.PlayerTextColor, PlayerTextColor);
        AdminTextColor = await settings.GetAsync(SettingKeys.AdminTextColor, AdminTextColor);
        SeasonTitle = await settings.GetAsync(SettingKeys.SeasonTitle, SeasonTitle);
    }
}
