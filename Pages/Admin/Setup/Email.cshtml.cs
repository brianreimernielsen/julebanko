using Julebanko.Data;
using Julebanko.Options;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Julebanko.Pages.Admin.Setup;

public class EmailModel(SettingsService settings, IOptions<SmtpOptions> smtpOptions) : PageModel
{
    [BindProperty] public string ClaimNotifyTo { get; set; } = "banko@example.com";
    [BindProperty] public string MailFrom { get; set; } = "julebanko@example.com";
    [BindProperty] public string SmtpHost { get; set; } = "outbound12.electric.net";
    [BindProperty] public int SmtpPort { get; set; } = 587;
    [BindProperty] public bool SmtpUseSsl { get; set; } = true;
    [BindProperty] public string SmtpUsername { get; set; } = "";
    [BindProperty] public string SmtpPassword { get; set; } = "";

    public bool PasswordIsSet { get; private set; }
    public bool Saved { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var file = smtpOptions.Value;
        if (SmtpPort <= 0)
        {
            SmtpPort = file.Port > 0 ? file.Port : 587;
        }

        var values = new Dictionary<string, string>
        {
            [SettingKeys.ClaimNotifyTo] = ClaimNotifyTo.Trim(),
            [SettingKeys.MailFrom] = MailFrom.Trim(),
            [SettingKeys.SmtpHost] = string.IsNullOrWhiteSpace(SmtpHost)
                ? (string.IsNullOrWhiteSpace(file.Host) ? "outbound12.electric.net" : file.Host)
                : SmtpHost.Trim(),
            [SettingKeys.SmtpPort] = SmtpPort.ToString(),
            [SettingKeys.SmtpUseSsl] = SmtpUseSsl ? "true" : "false",
            [SettingKeys.SmtpUsername] = SmtpUsername?.Trim() ?? ""
        };

        // Blank password keeps the existing stored value (or file default if never set).
        if (!string.IsNullOrEmpty(SmtpPassword))
        {
            values[SettingKeys.SmtpPassword] = SmtpPassword;
        }

        await settings.SetManyAsync(values);
        Saved = true;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var file = smtpOptions.Value;
        ClaimNotifyTo = await settings.GetAsync(SettingKeys.ClaimNotifyTo, ClaimNotifyTo);
        MailFrom = await settings.GetAsync(SettingKeys.MailFrom,
            string.IsNullOrWhiteSpace(file.From) ? MailFrom : file.From);
        SmtpHost = await settings.GetAsync(SettingKeys.SmtpHost,
            string.IsNullOrWhiteSpace(file.Host) ? SmtpHost : file.Host);
        var portRaw = await settings.GetAsync(SettingKeys.SmtpPort, file.Port.ToString());
        if (!int.TryParse(portRaw, out var port) || port <= 0)
        {
            port = file.Port > 0 ? file.Port : 587;
        }

        SmtpPort = port;
        var sslRaw = await settings.GetAsync(SettingKeys.SmtpUseSsl, file.UseSsl ? "true" : "false");
        SmtpUseSsl = !string.Equals(sslRaw, "false", StringComparison.OrdinalIgnoreCase) && sslRaw != "0";
        SmtpUsername = await settings.GetAsync(SettingKeys.SmtpUsername, file.Username ?? "");
        var storedPassword = await settings.GetAsync(SettingKeys.SmtpPassword, file.Password ?? "");
        PasswordIsSet = !string.IsNullOrEmpty(storedPassword);
        SmtpPassword = ""; // never echo password back into the form
    }
}
