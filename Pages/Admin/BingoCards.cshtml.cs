using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin;

public class BingoCardsModel(BingoCardService bingo, SettingsService settings) : PageModel
{
    /// <summary>Number of PDF pages to generate. Total plates = PageCount × PlatesPerPage.</summary>
    [BindProperty]
    public int PageCount { get; set; } = 4;

    [BindProperty]
    public int PlatesPerPage { get; set; } = 6;

    [BindProperty]
    public string? BannerText { get; set; }

    [BindProperty]
    public string? Note { get; set; }

    public IReadOnlyList<Models.BingoCardBatch> Batches { get; private set; } = Array.Empty<Models.BingoCardBatch>();
    public string? Error { get; private set; }
    public bool IsSetup => AdminAuthService.IsSetupAdmin(User);

    // Setup settings (bound only when setup posts settings)
    [BindProperty] public bool BingoAutoValidate { get; set; } = true;
    [BindProperty] public bool BingoPrizeOncePerType { get; set; } = true;
    [BindProperty] public string BingoBannerText { get; set; } = "";
    [BindProperty] public string BingoPrimaryColor { get; set; } = "";
    [BindProperty] public string BingoAccentColor { get; set; } = "";
    [BindProperty] public bool BingoFestiveLook { get; set; } = true;
    public bool SettingsSaved { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        try
        {
            var adminId = AdminAuthService.GetUserId(User) ?? 0;
            var platesPerPage = PlatesPerPage is 1 or 2 or 3 or 4 or 6 or 8 or 9 ? PlatesPerPage : 6;
            var pageCount = PageCount < 1 ? 1 : PageCount;
            var plateCount = checked(pageCount * platesPerPage);
            if (plateCount > 500)
            {
                Error = $"Too many plates ({plateCount}). Max is 500 (pages × plates per page).";
                await LoadAsync();
                return Page();
            }

            var (batch, pdfBytes) = await bingo.GenerateBatchAsync(
                plateCount, platesPerPage, adminId, BannerText, Note);

            var fileName = $"bankoplader-{batch.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnGetDownloadAsync(int batchId)
    {
        var pdf = await bingo.GetBatchPdfAsync(batchId);
        if (pdf is null)
        {
            return NotFound();
        }

        return File(pdf, "application/pdf", $"bankoplader-batch-{batchId}.pdf");
    }

    public async Task<IActionResult> OnPostSaveSettingsAsync()
    {
        if (!IsSetup)
        {
            return Forbid();
        }

        await settings.SetManyAsync(new Dictionary<string, string>
        {
            [Data.SettingKeys.BingoAutoValidate] = BingoAutoValidate ? "true" : "false",
            [Data.SettingKeys.BingoPrizeOncePerType] = BingoPrizeOncePerType ? "true" : "false",
            [Data.SettingKeys.BingoBannerText] = BingoBannerText?.Trim() ?? "",
            [Data.SettingKeys.BingoPrimaryColor] = BingoPrimaryColor?.Trim() ?? "",
            [Data.SettingKeys.BingoAccentColor] = BingoAccentColor?.Trim() ?? "",
            [Data.SettingKeys.BingoFestiveLook] = BingoFestiveLook ? "true" : "false"
        });
        SettingsSaved = true;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Batches = await bingo.ListBatchesAsync();

        var auto = await settings.GetAsync(Data.SettingKeys.BingoAutoValidate, "true");
        BingoAutoValidate = !string.Equals(auto, "false", StringComparison.OrdinalIgnoreCase) && auto != "0";
        var once = await settings.GetAsync(Data.SettingKeys.BingoPrizeOncePerType, "true");
        BingoPrizeOncePerType = !string.Equals(once, "false", StringComparison.OrdinalIgnoreCase) && once != "0";
        BingoBannerText = await settings.GetAsync(Data.SettingKeys.BingoBannerText, "");
        BingoPrimaryColor = await settings.GetAsync(Data.SettingKeys.BingoPrimaryColor, "");
        BingoAccentColor = await settings.GetAsync(Data.SettingKeys.BingoAccentColor, "");
        var festive = await settings.GetAsync(Data.SettingKeys.BingoFestiveLook, "true");
        BingoFestiveLook = !string.Equals(festive, "false", StringComparison.OrdinalIgnoreCase) && festive != "0";
    }
}
