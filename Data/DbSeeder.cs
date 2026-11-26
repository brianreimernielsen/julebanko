using Julebanko.Models;
using Julebanko.Options;
using Julebanko.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
        var smtp = scope.ServiceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value;
        await EnsureSettingAsync(db, SettingKeys.ClaimNotifyTo, "banko@example.com");
        await EnsureSettingAsync(db, SettingKeys.MailFrom,
            string.IsNullOrWhiteSpace(smtp.From) ? "julebanko@example.com" : smtp.From);
        await EnsureSettingAsync(db, SettingKeys.SmtpHost,
            string.IsNullOrWhiteSpace(smtp.Host) ? "outbound12.electric.net" : smtp.Host);
        await EnsureSettingAsync(db, SettingKeys.SmtpPort, smtp.Port > 0 ? smtp.Port.ToString() : "587");
        await EnsureSettingAsync(db, SettingKeys.SmtpUseSsl, smtp.UseSsl ? "true" : "false");
        await EnsureSettingAsync(db, SettingKeys.SmtpUsername, smtp.Username ?? "");
        await EnsureSettingAsync(db, SettingKeys.SmtpPassword, smtp.Password ?? "");
        await EnsureSettingAsync(db, SettingKeys.SeasonTitle, $"Julebanko {year}");
        await EnsureSettingAsync(db, SettingKeys.PlayerSnowEnabled, "true");
        await EnsureSettingAsync(db, SettingKeys.PlayerSnowIntensity, "1");
        await EnsureSettingAsync(db, SettingKeys.PlayerSnowGroundEnabled, "true");
        await EnsureSettingAsync(db, SettingKeys.PlayerNisseEnabled, "true");
        await EnsureSettingAsync(db, SettingKeys.DefaultCulture, "da");
        await EnsureSettingAsync(db, SettingKeys.BrandNameDa, CultureSupport.DefaultBrandDa);
        await EnsureSettingAsync(db, SettingKeys.BrandNameEn, CultureSupport.DefaultBrandEn);
        await EnsureSettingAsync(db, SettingKeys.BrandNameSv, CultureSupport.DefaultBrandSv);
        await EnsureSettingAsync(db, SettingKeys.BrandFont, FontCatalog.DefaultBrandFont);
        await EnsureSettingAsync(db, SettingKeys.BodyFont, FontCatalog.DefaultBodyFont);
        await EnsureSettingAsync(db, SettingKeys.BingoAutoValidate, "true");
        await EnsureSettingAsync(db, SettingKeys.BingoPrizeOncePerType, "true");
        await EnsureSettingAsync(db, SettingKeys.BingoBannerText, "");
        await EnsureSettingAsync(db, SettingKeys.BingoPrimaryColor, "");
        await EnsureSettingAsync(db, SettingKeys.BingoAccentColor, "");
        await EnsureSettingAsync(db, SettingKeys.BingoFestiveLook, "true");
        await EnsureSettingAsync(db, SettingKeys.BingoPlateScheme, BingoPlateSchemes.DefaultId);
        await EnsureSettingAsync(db, SettingKeys.BingoPdfBackgroundColor, "#FFFFFF");
        await EnsureSettingAsync(db, SettingKeys.BingoFrameColor, "#8B1E1E");
        await EnsureSettingAsync(db, SettingKeys.BingoBannerColor, "#8B1E1E");
        await EnsureSettingAsync(db, SettingKeys.BingoShowBarcode, "true");

        await db.SaveChangesAsync(cancellationToken);

        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        await EnsureSampleMediaAsync(db, env, logger, cancellationToken);
    }

    private static async Task EnsureSettingAsync(AppDbContext db, string key, string value)
    {
        if (!await db.AppSettings.AnyAsync(s => s.Key == key))
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
    }

    /// <summary>
    /// Copies bundled sample festive images into App_Data/uploads when a slot is empty.
    /// Setup Admin can replace/delete these later.
    /// </summary>
    private static async Task EnsureSampleMediaAsync(
        AppDbContext db,
        IWebHostEnvironment env,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var samples = MediaService.SampleSlots;

        var sampleRoot = Path.Combine(env.WebRootPath, "sample-media");
        var uploadRoot = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(uploadRoot);

        foreach (var (slot, fileName, contentType) in samples)
        {
            if (await db.MediaAssets.AnyAsync(m => m.Slot == slot, cancellationToken))
            {
                continue;
            }

            var source = Path.Combine(sampleRoot, fileName);
            if (!File.Exists(source))
            {
                logger.LogWarning("Sample media missing: {Path}", source);
                continue;
            }

            var destName = $"{slot.ToString().ToLowerInvariant()}-sample{Path.GetExtension(fileName)}";
            var destPath = Path.Combine(uploadRoot, destName);
            File.Copy(source, destPath, overwrite: true);

            db.MediaAssets.Add(new MediaAsset
            {
                Slot = slot,
                FileName = fileName,
                ContentType = contentType,
                RelativePath = $"/uploads/{destName}",
                UploadedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seeded sample media for slot {Slot}", slot);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
