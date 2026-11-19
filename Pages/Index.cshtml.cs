using System.Globalization;
using Julebanko.Data;
using Julebanko.Models;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages;

public class IndexModel(
    DrawService draws,
    ClaimService claims,
    SettingsService settings,
    MediaService media,
    SeasonService seasons) : PageModel
{
    [BindProperty]
    public string PlayerName { get; set; } = string.Empty;

    [BindProperty]
    public WinType WinType { get; set; } = WinType.OneRow;

    public string SeasonTitle { get; private set; } = "Julebanko";
    public string BrandName { get; private set; } = CultureSupport.DefaultBrandDa;
    public IReadOnlyList<DrawnNumber> TodayNumbers { get; private set; } = Array.Empty<DrawnNumber>();
    public IReadOnlyList<DrawnNumber> AllDrawOrder { get; private set; } = Array.Empty<DrawnNumber>();
    public IReadOnlyList<int> AllSorted { get; private set; } = Array.Empty<int>();
    public IReadOnlyList<Claim> ApprovedWinners { get; private set; } = Array.Empty<Claim>();
    public string? HeroUrl { get; private set; }
    public string? BackgroundUrl { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? StatusMessage { get; private set; }
    public bool StatusIsError { get; private set; }

    public async Task OnGetAsync(string? status = null)
    {
        await LoadAsync();
        if (status == "ok")
        {
            StatusMessage = "ok";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await claims.SubmitAsync(PlayerName, WinType);
        if (!result.Ok)
        {
            await LoadAsync();
            StatusMessage = result.Error;
            StatusIsError = true;
            return Page();
        }

        return RedirectToPage(new { status = "ok" });
    }

    private async Task LoadAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        SeasonTitle = await settings.GetAsync(SettingKeys.SeasonTitle, season.Title);
        BrandName = await settings.GetBrandNameAsync();
        AllDrawOrder = await draws.GetAllAsync();
        TodayNumbers = await draws.GetTodaysAsync();
        AllSorted = AllDrawOrder.Select(n => n.Number).OrderBy(n => n).ToList();
        ApprovedWinners = await claims.GetApprovedAsync();
        var assets = await media.GetAllAsync();
        if (assets.TryGetValue(MediaSlot.PlayerHero, out var hero))
        {
            HeroUrl = hero.RelativePath;
        }

        if (assets.TryGetValue(MediaSlot.PlayerBackground, out var bg))
        {
            BackgroundUrl = bg.RelativePath;
        }

        if (assets.TryGetValue(MediaSlot.PlayerHeroLogo, out var logo))
        {
            LogoUrl = logo.RelativePath;
        }
    }

    public string WinLabel(WinType type) =>
        ClaimService.WinTypeLabel(type, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
