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
    [BindProperty] public string BingoPdfBackgroundColor { get; set; } = BingoPdfService.DefaultParchmentHex;
    [BindProperty] public string BingoFrameColor { get; set; } = "#8B1E1E";
    [BindProperty] public string BingoBannerColor { get; set; } = "#8B1E1E";
    [BindProperty] public string BingoPdfTitleFont { get; set; } = BingoPdfTitleFonts.DefaultFontId;
    [BindProperty] public int BingoPdfTitleSize { get; set; } = BingoPdfTitleFonts.DefaultSize;
    [BindProperty] public bool BingoShowBarcode { get; set; } = true;
    /// <summary>Default OFF — page numbers are optional (Brian).</summary>
    [BindProperty] public bool BingoShowPageNumbers { get; set; }
    public MediaAsset? PdfBackgroundAsset { get; private set; }
    public MediaAsset? HeaderIconLeftAsset { get; private set; }
    public MediaAsset? HeaderIconRightAsset { get; private set; }
    public MediaAsset? PageBgDaAsset { get; private set; }
    public MediaAsset? PageBgSvAsset { get; private set; }
    public MediaAsset? PageBgEnAsset { get; private set; }
    public string HeaderIconLeftDefaultUrl => "/sample-media/bankoplade-header-icon-left.png";
    public string HeaderIconRightDefaultUrl => "/sample-media/bankoplade-header-icon-right.png";
    public string PageBgDaDefaultUrl => "/sample-media/bankoplade-page-bg-da.png";
    public string PageBgSvDefaultUrl => "/sample-media/bankoplade-page-bg-sv.png";
    public string PageBgEnDefaultUrl => "/sample-media/bankoplade-page-bg-en.png";
    public bool SettingsSaved { get; private set; }
    public string? MediaMessage { get; private set; }
    public bool MediaError { get; private set; }

    public IReadOnlyList<BingoPlateScheme> PlateSchemes => BingoPlateSchemes.All;
    public IReadOnlyList<BingoPdfTitleFonts.FontOption> TitleFonts => BingoPdfTitleFonts.Fonts;
    public IReadOnlyList<int> TitleSizes => BingoPdfTitleFonts.Sizes;
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
            var platesPerPage = PlatesPerPage is 1 or 2 or 3 or 4 or 6 ? PlatesPerPage : 6;
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

    /// <summary>
    /// Inline PDF stream for in-browser preview. No batch/card rows; dummy series only.
    /// Query params override saved settings so unsaved Design form values are reflected.
    /// </summary>
    public async Task<IActionResult> OnGetPreviewAsync(
        int platesPerPage = 6,
        string? banner = null,
        string? scheme = null,
        string? frame = null,
        string? bannerColor = null,
        string? bg = null,
        string? titleFont = null,
        int? titleSize = null,
        bool? showBarcode = null,
        bool? showPageNumbers = null,
        bool download = false)
    {
        platesPerPage = platesPerPage is 1 or 2 or 3 or 4 or 6 ? platesPerPage : 6;

        var style = new BingoPdfStyleOverrides
        {
            PlateScheme = string.IsNullOrWhiteSpace(scheme) ? null : scheme.Trim(),
            FrameColor = NormalizeOptionalHex(frame),
            BannerColor = NormalizeOptionalHex(bannerColor),
            BackgroundColor = NormalizeOptionalHex(bg),
            TitleFont = string.IsNullOrWhiteSpace(titleFont) ? null : BingoPdfTitleFonts.Resolve(titleFont).Id,
            TitleSize = titleSize is null ? null : BingoPdfTitleFonts.ResolveSize(titleSize.Value),
            ShowBarcode = showBarcode,
            ShowPageNumbers = showPageNumbers
        };

        var pdfBytes = await bingo.BuildPreviewPdfAsync(platesPerPage, banner, style);
        if (download)
        {
            return File(pdfBytes, "application/pdf", $"bankoplader-preview-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
        }

        // Inline so <iframe>/<object> can display without forcing a download.
        Response.Headers.CacheControl = "no-store";
        return File(pdfBytes, "application/pdf");
    }

    private static string? NormalizeOptionalHex(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return ColorHex.TryNormalize(raw, out var n) ? n : null;
    }

    public async Task<IActionResult> OnPostSaveSettingsAsync()
    {
        if (!IsSetup)
        {
            return Forbid();
        }

        var scheme = BingoPlateSchemes.GetOrDefault(BingoPlateScheme);
        var bg = ColorHex.OrDefault(BingoPdfBackgroundColor, BingoPdfService.DefaultParchmentHex);
        var frame = ColorHex.OrDefault(BingoFrameColor, scheme.ColorA);
        var banner = ColorHex.OrDefault(BingoBannerColor, frame);
        var titleFont = BingoPdfTitleFonts.Resolve(BingoPdfTitleFont).Id;
        var titleSize = BingoPdfTitleFonts.ResolveSize(BingoPdfTitleSize);

        await settings.SetManyAsync(new Dictionary<string, string>
        {
            [Data.SettingKeys.BingoAutoValidate] = BingoAutoValidate ? "true" : "false",
            [Data.SettingKeys.BingoPrizeOncePerType] = BingoPrizeOncePerType ? "true" : "false",
            [Data.SettingKeys.BingoBannerText] = BingoBannerText?.Trim() ?? "",
            [Data.SettingKeys.BingoPlateScheme] = scheme.Id,
            [Data.SettingKeys.BingoPdfBackgroundColor] = bg,
            [Data.SettingKeys.BingoFrameColor] = frame,
            [Data.SettingKeys.BingoBannerColor] = banner,
            [Data.SettingKeys.BingoPdfTitleFont] = titleFont,
            [Data.SettingKeys.BingoPdfTitleSize] = titleSize.ToString(),
            [Data.SettingKeys.BingoShowBarcode] = BingoShowBarcode ? "true" : "false",
            [Data.SettingKeys.BingoShowPageNumbers] = BingoShowPageNumbers ? "true" : "false"
        });
        SettingsSaved = true;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUploadHeaderIconAsync(MediaSlot slot, IFormFile? file)
    {
        if (!IsSetup)
        {
            return Forbid();
        }

        if (slot is not (MediaSlot.BingoHeaderIconLeft or MediaSlot.BingoHeaderIconRight))
        {
            MediaMessage = "InvalidSlot";
            MediaError = true;
            await LoadAsync();
            return Page();
        }

        if (file is null)
        {
            MediaMessage = "No file";
            MediaError = true;
            await LoadAsync();
            return Page();
        }

        var adminId = AdminAuthService.GetUserId(User);
        var result = await media.UploadAsync(slot, file, adminId);
        MediaMessage = result.Ok ? "ok" : result.Error;
        MediaError = !result.Ok;
        if (result.Ok)
        {
            SettingsSaved = true;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostResetHeaderIconAsync(MediaSlot slot)
    {
        if (!IsSetup)
        {
            return Forbid();
        }

        if (slot is not (MediaSlot.BingoHeaderIconLeft or MediaSlot.BingoHeaderIconRight))
        {
            MediaMessage = "InvalidSlot";
            MediaError = true;
            await LoadAsync();
            return Page();
        }

        var adminId = AdminAuthService.GetUserId(User);
        var result = await media.ResetBingoHeaderIconAsync(slot, adminId);
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

    public async Task<IActionResult> OnPostUploadPageBackgroundAsync(MediaSlot slot, IFormFile? file)
    {
        if (!IsSetup)
        {
            return Forbid();
        }

        if (slot is not (MediaSlot.BingoPageBackgroundDa or MediaSlot.BingoPageBackgroundSv or MediaSlot.BingoPageBackgroundEn))
        {
            MediaMessage = "InvalidSlot";
            MediaError = true;
            await LoadAsync();
            return Page();
        }

        if (file is null)
        {
            MediaMessage = "No file";
            MediaError = true;
            await LoadAsync();
            return Page();
        }

        var adminId = AdminAuthService.GetUserId(User);
        var result = await media.UploadAsync(slot, file, adminId);
        MediaMessage = result.Ok ? "ok" : result.Error;
        MediaError = !result.Ok;
        if (result.Ok)
        {
            SettingsSaved = true;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostResetPageBackgroundAsync(MediaSlot slot)
    {
        if (!IsSetup)
        {
            return Forbid();
        }

        if (slot is not (MediaSlot.BingoPageBackgroundDa or MediaSlot.BingoPageBackgroundSv or MediaSlot.BingoPageBackgroundEn))
        {
            MediaMessage = "InvalidSlot";
            MediaError = true;
            await LoadAsync();
            return Page();
        }

        var adminId = AdminAuthService.GetUserId(User);
        var result = await media.ResetBingoPageBackgroundAsync(slot, adminId);
        MediaMessage = result.Ok ? "ok" : result.Error;
        MediaError = !result.Ok;
        if (result.Ok)
        {
            SettingsSaved = true;
        }

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
            await settings.GetAsync(Data.SettingKeys.BingoPdfBackgroundColor, BingoPdfService.DefaultParchmentHex),
            BingoPdfService.DefaultParchmentHex);

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

        BingoPdfTitleFont = BingoPdfTitleFonts.Resolve(
            await settings.GetAsync(Data.SettingKeys.BingoPdfTitleFont, BingoPdfTitleFonts.DefaultFontId)).Id;
        BingoPdfTitleSize = BingoPdfTitleFonts.ResolveSize(
            await settings.GetAsync(Data.SettingKeys.BingoPdfTitleSize, BingoPdfTitleFonts.DefaultSize.ToString()));

        var showBarcodeRaw = await settings.GetAsync(Data.SettingKeys.BingoShowBarcode, "true");
        BingoShowBarcode = !string.Equals(showBarcodeRaw, "false", StringComparison.OrdinalIgnoreCase)
            && showBarcodeRaw != "0";

        // Default OFF when unset / false / 0.
        var showPagesRaw = await settings.GetAsync(Data.SettingKeys.BingoShowPageNumbers, "false");
        BingoShowPageNumbers = string.Equals(showPagesRaw, "true", StringComparison.OrdinalIgnoreCase)
            || showPagesRaw == "1";

        var assets = await media.GetAllAsync();
        assets.TryGetValue(MediaSlot.BingoPdfBackground, out var bgAsset);
        PdfBackgroundAsset = bgAsset;
        assets.TryGetValue(MediaSlot.BingoHeaderIconLeft, out var leftAsset);
        HeaderIconLeftAsset = leftAsset;
        assets.TryGetValue(MediaSlot.BingoHeaderIconRight, out var rightAsset);
        HeaderIconRightAsset = rightAsset;
        assets.TryGetValue(MediaSlot.BingoPageBackgroundDa, out var pageDa);
        PageBgDaAsset = pageDa;
        assets.TryGetValue(MediaSlot.BingoPageBackgroundSv, out var pageSv);
        PageBgSvAsset = pageSv;
        assets.TryGetValue(MediaSlot.BingoPageBackgroundEn, out var pageEn);
        PageBgEnAsset = pageEn;
    }
}
