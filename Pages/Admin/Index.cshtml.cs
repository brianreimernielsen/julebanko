using Julebanko.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin;

public class IndexModel(DrawService draws, ClaimService claims, SeasonService seasons) : PageModel
{
    public string SeasonTitle { get; private set; } = "";
    public int DrawnCount { get; private set; }
    public int PendingCount { get; private set; }
    public int ApprovedCount { get; private set; }
    public bool IsSetup { get; private set; }

    public async Task OnGetAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        SeasonTitle = season.Title;
        DrawnCount = (await draws.GetAllAsync()).Count;
        PendingCount = (await claims.GetPendingAsync()).Count;
        ApprovedCount = (await claims.GetApprovedAsync()).Count;
        IsSetup = AdminAuthService.IsSetupAdmin(User);
    }
}
