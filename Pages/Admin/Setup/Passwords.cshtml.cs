using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin.Setup;

public class PasswordsModel(AdminAuthService auth) : PageModel
{
    public IReadOnlyList<Models.AdminUser> Users { get; private set; } = Array.Empty<Models.AdminUser>();
    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync()
    {
        Users = await auth.ListUsersAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await auth.ChangePasswordByUsernameAsync(Username, NewPassword);
        Message = result.Ok ? "ok" : result.Error;
        IsError = !result.Ok;
        Users = await auth.ListUsersAsync();
        NewPassword = string.Empty;
        return Page();
    }
}
