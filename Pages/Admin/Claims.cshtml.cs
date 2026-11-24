using System.Globalization;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin;

public class ClaimsModel(ClaimService claims) : PageModel
{
    public IReadOnlyList<Models.Claim> Pending { get; private set; } = Array.Empty<Models.Claim>();
    public string? Message { get; private set; }

    public async Task OnGetAsync()
    {
        Pending = await claims.GetPendingAsync();
        if (TempData["ClaimMessage"] is string msg)
        {
            Message = msg;
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        var adminId = AdminAuthService.GetUserId(User) ?? 0;
        await claims.ReviewAsync(id, approve: true, adminId);
        TempData["ClaimMessage"] = "approved";
        // PRG so auto-refresh / reload cannot re-POST approve.
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        var adminId = AdminAuthService.GetUserId(User) ?? 0;
        await claims.ReviewAsync(id, approve: false, adminId);
        TempData["ClaimMessage"] = "rejected";
        return RedirectToPage();
    }

    public string WinLabel(Models.WinType type) =>
        ClaimService.WinTypeLabel(type, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public string ValidationLabel(Models.ClaimValidationStatus status) =>
        ClaimService.ValidationLabel(status, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
