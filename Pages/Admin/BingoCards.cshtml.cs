using System.Globalization;
using Julebanko.Models;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin;

public class BingoCardsModel(BingoCardService bingo, SettingsService settings, MediaService media) : PageModel
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
    [BindProperty] public string BingoPlateScheme { get; set; } = BingoPlateSchemes.DefaultId;
    [BindProperty] public string BingoPdfBackgroundColor { get; set; } = "#FFFFFF";
    [BindProperty] public string BingoFrameColor { get; set; } = "#8B1E1E";
    [BindProperty] public string BingoBannerColor { get; set; } = "#8B1E1E";
    public MediaAsset? PdfBackgroundAsset { get; private set; }
    public bool SettingsSaved { get; private set; }
    public string? MediaMessage { get; private set; }
    public bool MediaError { get; private set; }

    public IReadOnlyList<BingoPlateScheme> PlateSchemes => BingoPlateSchemes.All;
    public string UiCulture => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

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

        var scheme = BingoPlateSchemes.GetOrDefault(BingoPlateScheme);
        var bg = ColorHex.OrDefault(BingoPdfBackgroundColor, "#FFFFFF");
        var frame = ColorHex.OrDefault(BingoFrameColor, scheme.ColorA);
        var banner = ColorHex.OrDefault(BingoBannerColor, frame);

        await settings.SetManyAsync(new Dictionary<string, string>
        {
            [Data.SettingKeys.BingoAutoValidate] = BingoAutoValidate ? "true" : "false",
            [Data.SettingKeys.BingoPrizeOncePerType] = BingoPrizeOncePerType ? "true" : "false",
            [Data.SettingKeys.BingoBannerText] = BingoBannerText?.Trim() ?? "",
            [Data.SettingKeys.BingoPlateScheme] = scheme.Id,
            [Data.SettingKeys.BingoPdfBackgroundColor] = bg,
            [Data.SettingKeys.BingoFrameColor] = frame,
            [Data.SettingKeys.BingoBannerColor] = banner
        });
        SettingsSaved = true;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUploadPdfBackgroundAsync(IFormFile? file)
    {
        if (!IsSetup)
        {
            return Forbid();
        }

        if (file is null)
        {
            MediaMessage = "No file";
            MediaError = true;
            await LoadAsync();
            return Page();
        }

        var adminId = AdminAuthService.GetUserId(User);
        var result = await media.UploadAsync(MediaSlot.BingoPdfBackground, file, adminId);
        MediaMessage = result.Ok ? "ok" : result.Error;
        MediaError = !result.Ok;
        if (result.Ok)
        {
            SettingsSaved = true;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeletePdfBackgroundAsync()
    {
        if (!IsSetup)
        {
            return Forbid();
        }

        var result = await media.DeleteAsync(MediaSlot.BingoPdfBackground);
        MediaMessage = result.Ok ? "deleted" : result.Error;
        MediaError = !result.Ok;
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

        var schemeRaw = await settings.GetAsync(Data.SettingKeys.BingoPlateScheme, "");
        if (string.IsNullOrWhiteSpace(schemeRaw))
        {
            BingoPlateScheme = BingoPlateSchemes.DefaultId;
        }
        else
        {
            BingoPlateScheme = BingoPlateSchemes.GetOrDefault(schemeRaw).Id;
        }

        var scheme = BingoPlateSchemes.GetOrDefault(BingoPlateScheme);

        BingoPdfBackgroundColor = ColorHex.OrDefault(
            await settings.GetAsync(Data.SettingKeys.BingoPdfBackgroundColor, "#FFFFFF"),
            "#FFFFFF");

        var frameRaw = await settings.GetAsync(Data.SettingKeys.BingoFrameColor, "");
        if (string.IsNullOrWhiteSpace(frameRaw))
        {
            frameRaw = await settings.GetAsync(Data.SettingKeys.BingoPrimaryColor, scheme.ColorA);
        }

        BingoFrameColor = ColorHex.OrDefault(frameRaw, scheme.ColorA);

        var bannerRaw = await settings.GetAsync(Data.SettingKeys.BingoBannerColor, "");
        if (string.IsNullOrWhiteSpace(bannerRaw))
        {
            bannerRaw = BingoFrameColor;
        }

        BingoBannerColor = ColorHex.OrDefault(bannerRaw, BingoFrameColor);

        var assets = await media.GetAllAsync();
        assets.TryGetValue(MediaSlot.BingoPdfBackground, out var bgAsset);
        PdfBackgroundAsset = bgAsset;
    }
}
