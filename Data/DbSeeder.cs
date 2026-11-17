using Julebanko.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Data;

public static class DbSeeder
{
    public const string BankoAdminUsername = "bankoadmin";
    public const string SetupAdminUsername = "setupadmin";

    // Temporary passwords — change via Setup Admin after first login.
    public const string BankoAdminTempPassword = "BankoTemp2026!";
    public const string SetupAdminTempPassword = "SetupTemp2026!";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        if (string.Equals(Environment.GetEnvironmentVariable("JULEBANKO_USE_SQLITE"), "1", StringComparison.Ordinal))
        {
            // Preview/smoke path: skip SQL Server migrations and create schema directly.
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await db.Database.MigrateAsync(cancellationToken);
        }


        var hasher = new PasswordHasher<AdminUser>();

        if (!await db.AdminUsers.AnyAsync(u => u.Username == BankoAdminUsername))
        {
            var banko = new AdminUser
            {
                Username = BankoAdminUsername,
                Role = AdminRole.BankoAdmin,
                CreatedAt = DateTime.UtcNow
            };
            banko.PasswordHash = hasher.HashPassword(banko, BankoAdminTempPassword);
            db.AdminUsers.Add(banko);
            logger.LogInformation("Seeded Banko Admin user '{User}'", BankoAdminUsername);
        }

        if (!await db.AdminUsers.AnyAsync(u => u.Username == SetupAdminUsername))
        {
            var setup = new AdminUser
            {
                Username = SetupAdminUsername,
                Role = AdminRole.SetupAdmin,
                CreatedAt = DateTime.UtcNow
            };
            setup.PasswordHash = hasher.HashPassword(setup, SetupAdminTempPassword);
            db.AdminUsers.Add(setup);
            logger.LogInformation("Seeded Setup Admin user '{User}'", SetupAdminUsername);
        }

        var year = DateTime.UtcNow.Year;
        if (!await db.Seasons.AnyAsync(s => s.IsActive))
        {
            db.Seasons.Add(new Season
            {
                Year = year,
                Title = $"Julebanko {year}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await EnsureSettingAsync(db, SettingKeys.PlayerBgColor, "#8B1E1E");
        await EnsureSettingAsync(db, SettingKeys.AdminBgColor, "#1B4332");
        await EnsureSettingAsync(db, SettingKeys.AccentColor, "#D4A017");
        await EnsureSettingAsync(db, SettingKeys.PlayerTextColor, "#FFF8F0");
        await EnsureSettingAsync(db, SettingKeys.AdminTextColor, "#F0F7F2");
        await EnsureSettingAsync(db, SettingKeys.ClaimNotifyTo, "banko@example.com");
        await EnsureSettingAsync(db, SettingKeys.MailFrom, "julebanko@example.com");
        await EnsureSettingAsync(db, SettingKeys.SeasonTitle, $"Julebanko {year}");
        await EnsureSettingAsync(db, SettingKeys.PlayerSnowEnabled, "true");

        await db.SaveChangesAsync();
    }

    private static async Task EnsureSettingAsync(AppDbContext db, string key, string value)
    {
        if (!await db.AppSettings.AnyAsync(s => s.Key == key))
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
    }
}
