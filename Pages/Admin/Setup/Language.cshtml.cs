using Julebanko.Data;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin.Setup;

public class LanguageModel(SettingsService settings) : PageModel
{
    [BindProperty]
    public string DefaultCulture { get; set; } = CultureSupport.Danish;

    public bool Saved { get; private set; }

    public async Task OnGetAsync()
    {
        DefaultCulture = CultureSupport.Normalize(
            await settings.GetAsync(SettingKeys.DefaultCulture, CultureSupport.Danish));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        DefaultCulture = CultureSupport.Normalize(DefaultCulture);
        await settings.SetAsync(SettingKeys.DefaultCulture, DefaultCulture);
        Saved = true;
        return Page();
    }
}
