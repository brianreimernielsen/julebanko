using System.Globalization;
using Julebanko.Data;
using Julebanko.Options;
using Julebanko.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// SharedResource lives in namespace Julebanko; .resx under Resources/ embeds as Julebanko.SharedResource.*
builder.Services.AddLocalization();
builder.Services.AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/Admin");
        options.Conventions.AllowAnonymousToPage("/Admin/Login");
        options.Conventions.AuthorizeFolder("/Admin/Setup", "SetupAdmin");
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BankoAdmin", policy =>
        policy.RequireRole(
            AdminAuthService.BankoRole,
            AdminAuthService.SetupRole));
    options.AddPolicy("SetupAdmin", policy =>
        policy.RequireRole(AdminAuthService.SetupRole));
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.AccessDeniedPath = "/Admin/Login";
        options.Cookie.Name = "Julebanko.Auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    });

builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<DrawService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ClaimService>();
builder.Services.AddScoped<MediaService>();
builder.Services.AddScoped<SeasonResetService>();
builder.Services.AddScoped<AdminAuthService>();

var app = builder.Build();

var supportedCultures = new[] { "da", "en", "sv" };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("da"),
    SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList(),
    SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList()
};
localizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization(localizationOptions);

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads");
Directory.CreateDirectory(uploadsPath);
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data", "backups"));

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

try
{
    using var seedCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
    await DbSeeder.SeedAsync(app.Services, seedCts.Token);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogWarning(ex,
        "Database migrate/seed failed. Ensure SQL Server is reachable and the connection string is set. App will start; pages that need DB will fail until SQL is available.");
}

app.Run();

public partial class Program;
