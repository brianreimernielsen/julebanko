using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin;

public class LogoutModel(AdminAuthService auth) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await auth.SignOutAsync(HttpContext);
        return RedirectToPage("/Admin/Login");
    }
}
