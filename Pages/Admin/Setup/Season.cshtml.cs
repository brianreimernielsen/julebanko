using Julebanko;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Julebanko.Pages.Admin.Setup;

public class SeasonModel(
    SeasonResetService reset,
    SeasonService seasons,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public string SeasonTitle { get; private set; } = "";
    public IReadOnlyList<Models.SeasonBackup> Backups { get; private set; } = Array.Empty<Models.SeasonBackup>();
    [BindProperty] public string? Note { get; set; }
    [BindProperty] public string? PlatesNote { get; set; }
    /// <summary>Default true: season reset also deletes BingoCardBatches + BingoCards.</summary>
    [BindProperty] public bool IncludePlates { get; set; } = true;
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        IncludePlates = true;
        RestoreTempMessage();
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        var adminId = AdminAuthService.GetUserId(User) ?? 0;
        var result = await reset.ResetSeasonAsync(adminId, Note ?? "", IncludePlates);
        TempData["SeasonMessage"] = result.Ok
            ? (IncludePlates ? localizer["SeasonResetOkWithPlates"].Value : localizer["SeasonResetOk"].Value)
            : LocalizedError(result.Error);
        TempData["SeasonMessageError"] = result.Ok ? null : "1";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPlatesAsync()
    {
        var adminId = AdminAuthService.GetUserId(User) ?? 0;
        var result = await reset.ResetPlatesOnlyAsync(adminId, PlatesNote ?? "");
        TempData["SeasonMessage"] = result.Ok
            ? localizer["PlatesResetOk"].Value
            : LocalizedError(result.Error);
        TempData["SeasonMessageError"] = result.Ok ? null : "1";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUndoAsync(int id)
    {
        var result = await reset.UndoAsync(id);
        TempData["SeasonMessage"] = result.Ok
            ? localizer["BackupRestoredOk"].Value
            : LocalizedError(result.Error);
        TempData["SeasonMessageError"] = result.Ok ? null : "1";
        // PRG so refresh / admin auto-nav cannot re-POST restore.
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var season = await seasons.GetActiveSeasonAsync();
        SeasonTitle = season.Title;
        Backups = await reset.ListBackupsAsync();
    }

    private void RestoreTempMessage()
    {
        if (TempData["SeasonMessage"] is string msg)
        {
            Message = msg;
            IsError = TempData["SeasonMessageError"] is string;
        }
    }

    private string LocalizedError(string? code) => code switch
    {
        "NotFound" => localizer["BackupNotFound"].Value,
        "AlreadyRestored" => localizer["BackupAlreadyRestored"].Value,
        "FileMissing" => localizer["BackupFileMissing"].Value,
        "CorruptBackup" => localizer["BackupCorrupt"].Value,
        "SeasonMismatch" => localizer["BackupSeasonMismatch"].Value,
        _ => code ?? localizer["BackupRestoreFailed"].Value
    };
}
