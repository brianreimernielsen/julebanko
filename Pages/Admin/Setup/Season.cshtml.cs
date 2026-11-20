using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin.Setup;

public class SeasonModel(SeasonResetService reset, SeasonService seasons) : PageModel
{
    public string SeasonTitle { get; private set; } = "";
    public IReadOnlyList<Models.SeasonBackup> Backups { get; private set; } = Array.Empty<Models.SeasonBackup>();
    [BindProperty] public string Note { get; set; } = "";
    [BindProperty] public bool Confirm { get; set; }
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        if (!Confirm)
        {
            Message = "Confirm required";
            IsError = true;
            await LoadAsync();
            return Page();
        }

        var adminId = AdminAuthService.GetUserId(User) ?? 0;
        var result = await reset.ResetSeasonAsync(adminId, Note);
        Message = result.Ok ? "reset-ok" : result.Error;
        IsError = !result.Ok;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUndoAsync(int id)
    {
        var result = await reset.UndoAsync(id);
        Message = result.Ok ? "undo-ok" : result.Error;
        IsError = !result.Ok;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        SeasonTitle = season.Title;
        Backups = await reset.ListBackupsAsync();
    }
}
