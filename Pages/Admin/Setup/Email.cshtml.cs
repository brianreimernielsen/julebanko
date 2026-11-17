using Julebanko.Data;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin.Setup;

public class EmailModel(SettingsService settings) : PageModel
{
    [BindProperty] public string ClaimNotifyTo { get; set; } = "banko@example.com";
    [BindProperty] public string MailFrom { get; set; } = "julebanko@example.com";
    public bool Saved { get; private set; }

    public async Task OnGetAsync()
    {
        ClaimNotifyTo = await settings.GetAsync(SettingKeys.ClaimNotifyTo, ClaimNotifyTo);
        MailFrom = await settings.GetAsync(SettingKeys.MailFrom, MailFrom);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await settings.SetManyAsync(new Dictionary<string, string>
        {
            [SettingKeys.ClaimNotifyTo] = ClaimNotifyTo.Trim(),
            [SettingKeys.MailFrom] = MailFrom.Trim()
        });
        Saved = true;
        return Page();
    }
}
