using System.Globalization;
using Julebanko.Models;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin.Setup;

public class WinnersModel(ClaimService claims) : PageModel
{
    public IReadOnlyList<Claim> Winners { get; private set; } = Array.Empty<Claim>();
    [BindProperty] public int EditId { get; set; }
    [BindProperty] public string PlayerName { get; set; } = string.Empty;
    [BindProperty] public WinType WinType { get; set; }
    public string? Message { get; private set; }

    public async Task OnGetAsync()
    {
        Winners = await claims.GetApprovedAsync();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        var result = await claims.UpdateApprovedAsync(EditId, PlayerName, WinType);
        Message = result.Ok ? "saved" : result.Error;
        Winners = await claims.GetApprovedAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var result = await claims.DeleteApprovedAsync(id);
        Message = result.Ok ? "deleted" : result.Error;
        Winners = await claims.GetApprovedAsync();
        return Page();
    }

    public string WinLabel(WinType type) =>
        ClaimService.WinTypeLabel(type, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
