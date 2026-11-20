using Julebanko.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin;

[AllowAnonymous]
public class LoginModel(AdminAuthService auth) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            Response.Redirect("/Admin");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var (ok, user) = await auth.ValidateAsync(Username, Password);
        if (!ok || user is null)
        {
            ErrorMessage = "invalid";
            return Page();
        }

        await auth.SignInAsync(HttpContext, user);
        return RedirectToPage("/Admin/Index");
    }
}
