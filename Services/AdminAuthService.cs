using Julebanko.Data;
using Julebanko.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SysClaim = System.Security.Claims.Claim;
using SysClaimTypes = System.Security.Claims.ClaimTypes;
using SysClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using SysClaimsIdentity = System.Security.Claims.ClaimsIdentity;

namespace Julebanko.Services;

public class AdminAuthService(AppDbContext db)
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string RoleClaim = SysClaimTypes.Role;
    public const string BankoRole = nameof(AdminRole.BankoAdmin);
    public const string SetupRole = nameof(AdminRole.SetupAdmin);

    private readonly PasswordHasher<AdminUser> _hasher = new();

    public async Task<(bool Ok, AdminUser? User)> ValidateAsync(string username, string password)
    {
        username = (username ?? string.Empty).Trim();
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null)
        {
            return (false, null);
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? string.Empty);
        if (result == PasswordVerificationResult.Failed)
        {
            return (false, null);
        }

        return (true, user);
    }

    public async Task SignInAsync(HttpContext http, AdminUser user)
    {
        var claims = new List<SysClaim>
        {
            new(SysClaimTypes.NameIdentifier, user.Id.ToString()),
            new(SysClaimTypes.Name, user.Username),
            new(RoleClaim, user.Role.ToString())
        };

        // SetupAdmin can do everything BankoAdmin can.
        if (user.Role == AdminRole.SetupAdmin)
        {
            claims.Add(new SysClaim(RoleClaim, BankoRole));
        }

        var identity = new SysClaimsIdentity(claims, Scheme);
        var principal = new SysClaimsPrincipal(identity);
        await http.SignInAsync(Scheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
        });
    }

    public Task SignOutAsync(HttpContext http) => http.SignOutAsync(Scheme);

    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(int userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return (false, "PasswordTooShort");
        }

        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return (false, "NotFound");
        }

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ChangePasswordByUsernameAsync(string username, string newPassword)
    {
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null)
        {
            return (false, "NotFound");
        }

        return await ChangePasswordAsync(user.Id, newPassword);
    }

    public async Task<IReadOnlyList<AdminUser>> ListUsersAsync()
    {
        return await db.AdminUsers.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
    }

    public static int? GetUserId(SysClaimsPrincipal user)
    {
        var id = user.FindFirst(SysClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(id, out var value) ? value : null;
    }

    public static bool IsSetupAdmin(SysClaimsPrincipal user) =>
        user.IsInRole(SetupRole);

    public static bool IsBankoAdmin(SysClaimsPrincipal user) =>
        user.IsInRole(BankoRole) || user.IsInRole(SetupRole);
}
