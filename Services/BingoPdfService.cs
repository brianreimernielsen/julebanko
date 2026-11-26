using Julebanko.Data;
using Julebanko.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Julebanko.Services;

/// <summary>
/// Bankoplade PDF — two Design layouts (Brian / JUL-42):
/// Option 1 (default): full-page per-language background; plates overlaid in parchment center.
/// Option 2: composed live brand + L/R icons + straight title (no arch).
/// Max 6 plates/page, single column.
/// </summary>
public class BingoPdfService(SettingsService settings, MediaService media)
{
    /// <summary>Parchment sampled from Option 1 DA bg (~#FCF3E4).</summary>
    public const string DefaultParchmentHex = "#FCF3E4";

    /// <summary>Gold accent (scheme ColorB / flourish).</summary>
    public const string GoldAccentHex = "#C9A227";

    /// <summary>Deep festive red sampled from Option 1 frame (~#AA1F0D) — plate header + border.</summary>
    public const string FestiveRedHex = "#AA1F0D";

    /// <summary>
    /// Cream-tinted festive red for blank grid cells (distinct from header banner).
    /// Mix of <see cref="FestiveRedHex"/> toward parchment (~35%).
    /// </summary>
    public const string DefaultEmptyCellHex = "#C46852";

    public const string LayoutOption1 = "1";
    public const string LayoutOption2 = "2";

    static BingoPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        BingoPdfTitleFonts.EnsureRegistered(AppContext.BaseDirectory);
    }

    public static string NormalizeLayoutOption(string? raw) =>
        string.Equals(raw?.Trim(), LayoutOption2, StringComparison.Ordinal)
            ? LayoutOption2
            : LayoutOption1;

    public Task<byte[]> BuildPdfAsync(IReadOnlyList<BingoCard> cards, int platesPerPage, string? batchBanner)
        => BuildPdfAsync(cards, platesPerPage, batchBanner, styleOverrides: null);

    public async Task<byte[]> BuildPdfAsync(
        IReadOnlyList<BingoCard> cards,
        int platesPerPage,
        string? batchBanner,
        BingoPdfStyleOverrides? styleOverrides)
    {
        platesPerPage = platesPerPage is 1 or 2 or 3 or 4 or 6 ? platesPerPage : 6;

        var culture = CultureSupport.Normalize(
            await settings.GetAsync(SettingKeys.DefaultCulture, CultureSupport.Danish));
        var brand = await settings.GetBrandNameOrFallbackAsync(culture);

        var banner = !string.IsNullOrWhiteSpace(batchBanner)
            ? batchBanner.Trim()
            : await settings.GetAsync(SettingKeys.BingoBannerText, "");
        if (string.IsNullOrWhiteSpace(banner))
        {
            banner = brand;
        }

        var titleFontId = !string.IsNullOrWhiteSpace(styleOverrides?.TitleFont)
            ? BingoPdfTitleFonts.Resolve(styleOverrides!.TitleFont).Id
            : BingoPdfTitleFonts.Resolve(
                await settings.GetAsync(SettingKeys.BingoPdfTitleFont, BingoPdfTitleFonts.DefaultFontId)).Id;
        var titleSize = styleOverrides?.TitleSize is int overrideSize
            ? BingoPdfTitleFonts.ResolveSize(overrideSize)
            : BingoPdfTitleFonts.ResolveSize(
                await settings.GetAsync(SettingKeys.BingoPdfTitleSize, BingoPdfTitleFonts.DefaultSize.ToString()));

        var plateHeaderFontId = !string.IsNullOrWhiteSpace(styleOverrides?.PlateHeaderFont)
            ? BingoPdfTitleFonts.Resolve(styleOverrides!.PlateHeaderFont).Id
            : BingoPdfTitleFonts.Resolve(
                await settings.GetAsync(SettingKeys.BingoPlateHeaderFont, BingoPdfTitleFonts.DefaultPlateHeaderFontId)).Id;
        var plateNumberFontId = !string.IsNullOrWhiteSpace(styleOverrides?.PlateNumberFont)
            ? BingoPdfTitleFonts.Resolve(styleOverrides!.PlateNumberFont).Id
            : BingoPdfTitleFonts.Resolve(
                await settings.GetAsync(SettingKeys.BingoPlateNumberFont, BingoPdfTitleFonts.DefaultPlateNumberFontId)).Id;

        var schemeId = !string.IsNullOrWhiteSpace(styleOverrides?.PlateScheme)
            ? styleOverrides!.PlateScheme!
            : await settings.GetAsync(SettingKeys.BingoPlateScheme, BingoPlateSchemes.DefaultId);
        var scheme = BingoPlateSchemes.GetOrDefault(schemeId);

        var frameHex = !string.IsNullOrWhiteSpace(styleOverrides?.FrameColor)
            ? styleOverrides!.FrameColor!
            : await settings.GetAsync(SettingKeys.BingoFrameColor, "");
        if (string.IsNullOrWhiteSpace(frameHex))
        {
            frameHex = await settings.GetAsync(SettingKeys.BingoPrimaryColor, "");
            if (string.IsNullOrWhiteSpace(frameHex))
            {
                frameHex = scheme.ColorA;
            }
        }

        var bannerHex = !string.IsNullOrWhiteSpace(styleOverrides?.BannerColor)
            ? styleOverrides!.BannerColor!
            : await settings.GetAsync(SettingKeys.BingoBannerColor, "");
        if (string.IsNullOrWhiteSpace(bannerHex))
        {
            bannerHex = frameHex;
        }

        var bgHex = !string.IsNullOrWhiteSpace(styleOverrides?.BackgroundColor)
            ? styleOverrides!.BackgroundColor!
            : await settings.GetAsync(SettingKeys.BingoPdfBackgroundColor, DefaultParchmentHex);
        if (string.IsNullOrWhiteSpace(bgHex))
        {
            bgHex = DefaultParchmentHex;
        }

        var showBarcode = styleOverrides?.ShowBarcode
            ?? IsTruthy(await settings.GetAsync(SettingKeys.BingoShowBarcode, "true"));
        var showPageNumbers = styleOverrides?.ShowPageNumbers
            ?? IsTruthy(await settings.GetAsync(SettingKeys.BingoShowPageNumbers, "false"));

        var layoutOption = NormalizeLayoutOption(
            !string.IsNullOrWhiteSpace(styleOverrides?.LayoutOption)
                ? styleOverrides!.LayoutOption
                : await settings.GetAsync(SettingKeys.BingoPdfLayoutOption, LayoutOption1));

        // Header bar + plate border: Design frame color (falls back to scheme ColorA).
        var festiveRed = ParseColor(frameHex, ParseColor(scheme.ColorA, ParseColor(FestiveRedHex, Colors.Red.Darken3)));
        var titleColor = ParseColor(bannerHex, festiveRed);
        var paper = ParseColor(bgHex, ParseColor(DefaultParchmentHex, Colors.White));
        var gold = ParseColor(GoldAccentHex, Colors.Amber.Darken2);

        var emptyHex = !string.IsNullOrWhiteSpace(styleOverrides?.EmptyCellColor)
            ? styleOverrides!.EmptyCellColor!
            : await settings.GetAsync(SettingKeys.BingoEmptyCellColor, DefaultEmptyCellHex);
        if (string.IsNullOrWhiteSpace(emptyHex))
        {
            emptyHex = DefaultEmptyCellHex;
        }

        // Blank cells: separate shade from header (cream-tinted festive red by default).
        var emptyCell = ParseColor(emptyHex, ParseColor(DefaultEmptyCellHex, festiveRed));
        var ink = Color.FromHex("#1A1A1A");

        var leftIcon = await media.TryReadBingoHeaderIconAsync(MediaSlot.BingoHeaderIconLeft);
        var rightIcon = await media.TryReadBingoHeaderIconAsync(MediaSlot.BingoHeaderIconRight);
        byte[]? leftIconBytes = leftIcon?.Bytes;
        byte[]? rightIconBytes = rightIcon?.Bytes;

        var pageBg = await media.TryReadBingoPageBackgroundAsync(culture);
        byte[]? pageBgBytes = pageBg?.Bytes;

        // Option 1 needs a page bg (DA/SV/EN bundled samples, or upload). Without one → Option 2.
        var useOption1 = layoutOption == LayoutOption1 && pageBgBytes is { Length: > 0 };

        var pages = cards
            .Select((card, i) => (card, i))
            .GroupBy(x => x.i / platesPerPage)
            .Select(g => g.Select(x => x.card).ToList())
            .ToList();

        var plateScale = PlateScaleFor(platesPerPage);

        if (useOption1)
        {
            return BuildOption1Document(
                pages, platesPerPage, pageBgBytes!, banner, brand, festiveRed, emptyCell, ink, paper,
                plateScale, showBarcode, showPageNumbers, plateHeaderFontId, plateNumberFontId);
        }

        return BuildOption2Document(
            pages, platesPerPage, banner, brand, titleFontId, titleSize, titleColor, festiveRed,
            emptyCell, ink, paper, gold, leftIconBytes, rightIconBytes, plateScale,
            showBarcode, showPageNumbers, plateHeaderFontId, plateNumberFontId);
    }

    /// <summary>
    /// Option 1: full-page bg (baked brand) + plates in the parchment well.
    /// </summary>
    private static byte[] BuildOption1Document(
        List<List<BingoCard>> pages,
        int platesPerPage,
        byte[] pageBgBytes,
        string banner,
        string brand,
        Color festiveRed,
        Color emptyCell,
        Color ink,
        Color paper,
        PlateScale plateScale,
        bool showBarcode,
        bool showPageNumbers,
        string plateHeaderFontId,
        string plateNumberFontId)
    {
        // Clear decorative title/footer bands; plates sit in the cream well with
        // roughly equal breathing room under the top banner and above the bottom banner
        // (shared margins across DA/SV/EN Option 1 art).
        const float marginLeft = 38f;
        const float marginRight = 38f;
        // Shared Option 1 margins: keep cream above first plate ≈ cream above bottom banner
        // across DA/SV/EN (title flourish vs spruce tips differ slightly per language).
        const float marginTop = 133f;
        const float marginBottom = 129f;
        const float gap = 4f;
        const float plateCornerRadius = 9f;

        var document = Document.Create(container =>
        {
            foreach (var pageCards in pages)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginLeft(marginLeft);
                    page.MarginRight(marginRight);
                    page.MarginTop(marginTop);
                    page.MarginBottom(marginBottom);
                    page.PageColor(paper);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(ink));
                    page.Background().Image(pageBgBytes).FitUnproportionally();

                    var footerPad = showPageNumbers ? 14f : 0f;
                    var available = PageSizes.A4.Height - marginTop - marginBottom - footerPad;
                    var gapsTotal = Math.Max(0, pageCards.Count - 1) * gap;
                    var plateHeight = (available - gapsTotal) / Math.Max(1, pageCards.Count);
                    var plateHeaderH = PlateHeaderHeight(plateScale, showBarcode);
                    var cellHeight = Math.Max(10f, (plateHeight - plateHeaderH - 2f) / BingoPlateGenerator.Rows);
                    var fillScale = plateScale with { CellMinHeight = cellHeight };

                    page.Content().Column(col =>
                    {
                        for (var i = 0; i < pageCards.Count; i++)
                        {
                            col.Item().Height(plateHeight).Element(e => RenderPlate(
                                e, pageCards[i], banner, festiveRed, emptyCell, ink, fillScale, showBarcode,
                                plateCornerRadius, plateHeaderFontId, plateNumberFontId));
                            if (i < pageCards.Count - 1)
                            {
                                col.Item().Height(gap);
                            }
                        }
                    });

                    if (showPageNumbers)
                    {
                        page.Footer().AlignCenter()
                            .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1))
                            .Text(text =>
                            {
                                text.Span($"{brand} ");
                                text.CurrentPageNumber();
                                text.Span("/");
                                text.TotalPages();
                            });
                    }
                });
            }
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// Option 2: live straight brand + L/R icons (no arch) on parchment / optional chrome.
    /// </summary>
    private static byte[] BuildOption2Document(
        List<List<BingoCard>> pages,
        int platesPerPage,
        string banner,
        string brand,
        string titleFontId,
        float titleSize,
        Color titleColor,
        Color festiveRed,
        Color emptyCell,
        Color ink,
        Color paper,
        Color gold,
        byte[]? leftIconBytes,
        byte[]? rightIconBytes,
        PlateScale plateScale,
        bool showBarcode,
        bool showPageNumbers,
        string plateHeaderFontId,
        string plateNumberFontId)
    {
        const float pageMargin = 14f;
        const float flourishHeight = 10f;
        var pageContentWidth = PageSizes.A4.Width - (2f * pageMargin);
        var titleLayout = ResolveHeaderTitleLayout(banner, titleSize, pageContentWidth);
        var headerIconHeight = titleLayout.IconHeight;
        var headerIconWidth = titleLayout.IconWidth;
        var titleChordWidth = titleLayout.TitleChordWidth;
        var iconTitleGap = titleLayout.IconTitleGap;
        var titleBandHeight = Math.Max(titleSize + 12f, headerIconHeight + 4f);
        var docHeaderHeight = titleBandHeight + flourishHeight + 4f;

        var document = Document.Create(container =>
        {
            foreach (var pageCards in pages)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(pageMargin);
                    page.PageColor(paper);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(ink));

                    page.Header().Column(headerCol =>
                    {
                        // Straight title (no arch) — long brands stay readable with scaled icons + gap.
                        headerCol.Item().Height(titleBandHeight).AlignMiddle().Row(header =>
                        {
                            header.RelativeItem();

                            header.ConstantItem(headerIconWidth).AlignMiddle().AlignCenter().Element(e =>
                            {
                                if (leftIconBytes is { Length: > 0 })
                                {
                                    e.Height(headerIconHeight).Image(leftIconBytes).FitArea();
                                }
                            });

                            header.ConstantItem(iconTitleGap);

                            header.ConstantItem(titleChordWidth).AlignMiddle().AlignCenter()
                                .Text(banner)
                                .FontFamily(titleFontId)
                                .FontSize(titleSize)
                                .SemiBold()
                                .FontColor(titleColor);

                            header.ConstantItem(iconTitleGap);

                            header.ConstantItem(headerIconWidth).AlignMiddle().AlignCenter().Element(e =>
                            {
                                if (rightIconBytes is { Length: > 0 })
                                {
                                    e.Height(headerIconHeight).Image(rightIconBytes).FitArea();
                                }
                            });

                            header.RelativeItem();
                        });

                        headerCol.Item().Height(flourishHeight).AlignCenter().AlignMiddle().Row(fl =>
                        {
                            fl.RelativeItem().AlignMiddle().LineHorizontal(0.8f).LineColor(gold);
                            fl.ConstantItem(14).AlignCenter().AlignMiddle()
                                .Text("✦").FontSize(8).FontColor(gold);
                            fl.RelativeItem().AlignMiddle().LineHorizontal(0.8f).LineColor(gold);
                        });
                    });

                    const float contentTopPad = 2f;
                    const float gap = 4f;
                    var footerEstimate = showPageNumbers ? 16f : 6f;
                    var available = PageSizes.A4.Height - (2 * pageMargin) - docHeaderHeight - footerEstimate - contentTopPad;
                    var gapsTotal = Math.Max(0, pageCards.Count - 1) * gap;
                    var plateHeight = (available - gapsTotal) / Math.Max(1, pageCards.Count);
                    var plateHeaderH = PlateHeaderHeight(plateScale, showBarcode);
                    var cellHeight = Math.Max(10f, (plateHeight - plateHeaderH - 2f) / BingoPlateGenerator.Rows);
                    var fillScale = plateScale with { CellMinHeight = cellHeight };

                    page.Content().PaddingTop(contentTopPad).Column(col =>
                    {
                        for (var i = 0; i < pageCards.Count; i++)
                        {
                            col.Item().Height(plateHeight).Element(e => RenderPlate(
                                e, pageCards[i], banner, festiveRed, emptyCell, ink, fillScale, showBarcode,
                                cornerRadius: 0f, plateHeaderFontId, plateNumberFontId));
                            if (i < pageCards.Count - 1)
                            {
                                col.Item().Height(gap);
                            }
                        }
                    });

                    if (showPageNumbers)
                    {
                        page.Footer().AlignCenter()
                            .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1))
                            .Text(text =>
                            {
                                text.Span($"{brand} ");
                                text.CurrentPageNumber();
                                text.Span("/");
                                text.TotalPages();
                            });
                    }
                });
            }
        });

        return document.GeneratePdf();
    }

    private readonly record struct HeaderTitleLayout(
        float TitleChordWidth,
        float IconWidth,
        float IconHeight,
        float IconTitleGap);

    /// <summary>Size [icon | title | icon] so long brands (e.g. Christmas Bingo) stay fully readable.</summary>
    private static HeaderTitleLayout ResolveHeaderTitleLayout(string banner, float titleSize, float pageContentWidth)
    {
        var len = Math.Max(1, banner.Length);
        var longTitle = len >= 12;
        var estimatedTitle = Math.Max(80f, len * titleSize * (longTitle ? 0.72f : 0.60f));
        var baseIconHeight = Math.Max(20f, titleSize + 2f);
        var baseIconWidth = baseIconHeight * 2.9f;
        var gap = longTitle ? 18f : 4f;
        var maxGroup = pageContentWidth * 0.96f;
        var iconScale = longTitle ? 0.48f : 1f;
        var titleChord = estimatedTitle;
        for (var iter = 0; iter < 4; iter++)
        {
            var iconWidthTry = baseIconWidth * iconScale;
            var group = (2f * iconWidthTry) + titleChord + (2f * gap);
            if (group <= maxGroup)
            {
                break;
            }

            var iconBudget = Math.Max(40f, maxGroup - titleChord - (2f * gap));
            iconScale = Math.Clamp(iconBudget / (2f * baseIconWidth), 0.36f, iconScale);
            var maxTitle = maxGroup - (2f * baseIconWidth * iconScale) - (2f * gap);
            titleChord = Math.Min(titleChord, Math.Max(80f, maxTitle));
        }

        var iconHeight = baseIconHeight * iconScale;
        var iconWidth = baseIconWidth * iconScale;
        titleChord = Math.Max(80f, Math.Min(titleChord, maxGroup - (2f * iconWidth) - (2f * gap)));
        return new HeaderTitleLayout(titleChord, iconWidth, iconHeight, gap);
    }

    private static float PlateHeaderHeight(PlateScale scale, bool showBarcode) =>
        Math.Max(scale.BannerFont + 6f, SeriesMarkerHeight(scale, showBarcode) + 4f);

    private static void RenderPlate(
        IContainer container,
        BingoCard card,
        string banner,
        Color festiveRed,
        Color emptyCell,
        Color ink,
        PlateScale scale,
        bool showBarcode,
        float cornerRadius,
        string plateHeaderFontId,
        string plateNumberFontId)
    {
        var grid = BingoPlateGenerator.FromJson(card.CellsJson);
        var seriesText = BingoPlateGenerator.FormatSeries(card.SeriesNumber);
        var hriFont = HriFontSize(scale);
        var hriLine = HriLineHeight(scale);
        var headerH = PlateHeaderHeight(scale, showBarcode);
        var barcodeCorner = cornerRadius > 0f ? Math.Max(2.5f, cornerRadius * 0.45f) : 0f;
        byte[]? barcodePng = null;
        if (showBarcode)
        {
            barcodePng = BingoSeriesBarcode.CreateCode128Png(
                seriesText,
                widthPx: (int)(scale.BarcodeWidth * 3f),
                heightPx: (int)(scale.BarcodeHeight * 3f));
        }

        var plate = container;
        if (cornerRadius > 0f)
        {
            plate = plate.CornerRadius(cornerRadius);
        }

        plate
            .Border(1.2f)
            .BorderColor(festiveRed)
            .Column(col =>
        {
            col.Item().Height(headerH).Background(festiveRed).PaddingHorizontal(6).Row(r =>
            {
                r.RelativeItem().AlignMiddle()
                    .Text(banner)
                    .FontFamily(plateHeaderFontId)
                    .FontSize(scale.BannerFont)
                    .SemiBold()
                    .FontColor(Colors.White);

                var barcodeBox = r.ConstantItem(scale.BarcodeWidth + 6f).AlignMiddle().AlignRight();
                if (barcodeCorner > 0f)
                {
                    barcodeBox = barcodeBox.CornerRadius(barcodeCorner);
                }

                barcodeBox
                    .Background(Colors.White)
                    .Padding(1)
                    .Column(bc =>
                    {
                        if (showBarcode && barcodePng is not null)
                        {
                            bc.Item().Height(scale.BarcodeHeight).AlignCenter()
                                .Image(barcodePng).FitHeight();
                        }

                        bc.Item().Height(hriLine).AlignCenter().AlignMiddle()
                            .Text(seriesText)
                            .FontFamily(plateNumberFontId)
                            .FontSize(hriFont)
                            .SemiBold()
                            .FontColor(Colors.Black);
                    });
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (var i = 0; i < BingoPlateGenerator.Cols; i++)
                    {
                        columns.RelativeColumn();
                    }
                });

                for (var row = 0; row < BingoPlateGenerator.Rows; row++)
                {
                    for (var c = 0; c < BingoPlateGenerator.Cols; c++)
                    {
                        var n = grid[row, c];
                        table.Cell()
                            .Border(0.55f)
                            .BorderColor(Color.FromHex("#5A1010"))
                            .Background(n is null ? emptyCell : Colors.White)
                            .MinHeight(scale.CellMinHeight)
                            .AlignMiddle()
                            .AlignCenter()
                            .Text(n?.ToString() ?? "")
                            .FontFamily(plateNumberFontId)
                            .FontSize(scale.CellFont)
                            .SemiBold()
                            .FontColor(ink);
                    }
                }
            });
        });
    }

    private readonly record struct PlateScale(
        float Padding,
        float BannerFont,
        float CellFont,
        float CellMinHeight,
        float TablePad,
        float LinePad,
        float BarcodeWidth,
        float BarcodeHeight);

    private static float HriFontSize(PlateScale scale) => Math.Max(7f, scale.BannerFont - 4f);
    private static float HriLineHeight(PlateScale scale) => HriFontSize(scale) + 3f;
    private static float SeriesMarkerHeight(PlateScale scale, bool showBarcode) =>
        showBarcode ? scale.BarcodeHeight + HriLineHeight(scale) : HriLineHeight(scale);

    private static bool IsTruthy(string? raw) =>
        !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) && raw != "0";

    private static PlateScale PlateScaleFor(int platesPerPage) => platesPerPage switch
    {
        1 => new PlateScale(0, 16, 22, 0, 0, 0, 120, 22),
        2 => new PlateScale(0, 14, 18, 0, 0, 0, 110, 20),
        3 => new PlateScale(0, 13, 15, 0, 0, 0, 100, 18),
        4 => new PlateScale(0, 12, 13, 0, 0, 0, 95, 16),
        _ => new PlateScale(0, 11, 11, 0, 0, 0, 90, 16)
    };

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            if (!ColorHex.TryNormalize(hex, out var n))
            {
                return fallback;
            }

            return Color.FromHex(n);
        }
        catch
        {
            return fallback;
        }
    }
}

/// <summary>Optional style overrides for preview PDF (does not persist settings).</summary>
public sealed class BingoPdfStyleOverrides
{
    public string? PlateScheme { get; init; }
    public string? FrameColor { get; init; }
    public string? BannerColor { get; init; }
    public string? EmptyCellColor { get; init; }
    public string? BackgroundColor { get; init; }
    public string? TitleFont { get; init; }
    public int? TitleSize { get; init; }
    /// <summary>Plate header brand font id from <see cref="BingoPdfTitleFonts"/>.</summary>
    public string? PlateHeaderFont { get; init; }
    /// <summary>Plate grid number font id from <see cref="BingoPdfTitleFonts"/>.</summary>
    public string? PlateNumberFont { get; init; }
    public bool? ShowBarcode { get; init; }
    public bool? ShowPageNumbers { get; init; }
    /// <summary>"1" or "2" — see <see cref="BingoPdfService.LayoutOption1"/>.</summary>
    public string? LayoutOption { get; init; }
}
