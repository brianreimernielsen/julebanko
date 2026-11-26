using Julebanko.Data;
using Julebanko.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Julebanko.Services;

/// <summary>
/// JUL-42 festive bankoplade PDF: cream parchment + gold frame (via per-culture page bg),
/// editable brand text overlaid on document header and plate header bars (never baked into images).
/// </summary>
public class BingoPdfService(SettingsService settings, MediaService media)
{
    /// <summary>Reference parchment when no page-bg image / color override.</summary>
    public const string DefaultParchmentHex = "#FEF6E3";

    /// <summary>Gold accent for under-title flourish (matches plate schemes / reference).</summary>
    public const string GoldAccentHex = "#C9A227";

    /// <summary>Deep Christmas red for plate header bars and empty cells (not candy-pink).</summary>
    public const string FestiveRedHex = "#8B1E1E";

    static BingoPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        BingoPdfTitleFonts.EnsureRegistered(AppContext.BaseDirectory);
    }

    public Task<byte[]> BuildPdfAsync(IReadOnlyList<BingoCard> cards, int platesPerPage, string? batchBanner)
        => BuildPdfAsync(cards, platesPerPage, batchBanner, styleOverrides: null);

    public async Task<byte[]> BuildPdfAsync(
        IReadOnlyList<BingoCard> cards,
        int platesPerPage,
        string? batchBanner,
        BingoPdfStyleOverrides? styleOverrides)
    {
        // Brian / JUL-42: max 6 plates per page — single full-width column only (no 8/9 grids).
        platesPerPage = platesPerPage is 1 or 2 or 3 or 4 or 6 ? platesPerPage : 6;

        var culture = CultureSupport.Normalize(
            await settings.GetAsync(SettingKeys.DefaultCulture, CultureSupport.Danish));
        var brand = await settings.GetBrandNameOrFallbackAsync(culture);

        // Document title + plate header brand: batch override → BingoBannerText → brand settings.
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

        var frameColor = ParseColor(frameHex, ParseColor(FestiveRedHex, Colors.Red.Darken3));
        var titleColor = ParseColor(bannerHex, frameColor);
        var paper = ParseColor(bgHex, ParseColor(DefaultParchmentHex, Colors.White));
        var gold = ParseColor(GoldAccentHex, Colors.Amber.Darken2);
        var festiveRed = ParseColor(scheme.ColorA, ParseColor(FestiveRedHex, Colors.Red.Darken3));
        // Reference: empty cells = deep Christmas red (not washed pink).
        var emptyCell = festiveRed;
        var ink = Color.FromHex("#1A1A1A");

        var leftIcon = await media.TryReadBingoHeaderIconAsync(MediaSlot.BingoHeaderIconLeft);
        var rightIcon = await media.TryReadBingoHeaderIconAsync(MediaSlot.BingoHeaderIconRight);
        byte[]? leftIconBytes = leftIcon?.Bytes;
        byte[]? rightIconBytes = rightIcon?.Bytes;

        // Per-culture decorative page bg (cream/gold/footer) — brand text drawn on top.
        var pageBg = await media.TryReadBingoPageBackgroundAsync(culture);
        byte[]? pageBgBytes = pageBg?.Bytes;

        var pages = cards
            .Select((card, i) => (card, i))
            .GroupBy(x => x.i / platesPerPage)
            .Select(g => g.Select(x => x.card).ToList())
            .ToList();

        var plateScale = PlateScaleFor(platesPerPage);
        var headerIconHeight = Math.Max(20f, titleSize + 2f);
        var headerIconWidth = headerIconHeight * 2.9f;
        const float headerIconOverlap = 6f;
        const float flourishHeight = 10f;
        // Extra height for arched title (ends sit lower than center).
        var arcAmplitude = titleSize * 0.42f;
        var titleBandHeight = Math.Max(titleSize + arcAmplitude + 10f, headerIconHeight + 4f);
        var docHeaderHeight = titleBandHeight + flourishHeight + 4f;
        // Approximate title chord width for icon placement beside the arc.
        var titleChordWidth = Math.Max(80f, banner.Length * titleSize * 0.58f);

        var document = Document.Create(container =>
        {
            foreach (var pageCards in pages)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(14);
                    page.PageColor(paper);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(ink));

                    if (pageBgBytes is { Length: > 0 })
                    {
                        // Decorative chrome only — no baked brand; live text overlays this.
                        page.Background().Image(pageBgBytes).FitUnproportionally();
                    }

                    page.Header().Column(headerCol =>
                    {
                        // Centered [icon | arched brand | icon] — brand is live editable text on an arc.
                        headerCol.Item().Height(titleBandHeight).AlignMiddle().Row(header =>
                        {
                            header.RelativeItem();

                            header.ConstantItem(headerIconWidth).AlignMiddle().AlignRight()
                                .TranslateX(headerIconOverlap).Element(e =>
                            {
                                if (leftIconBytes is { Length: > 0 })
                                {
                                    e.Height(headerIconHeight).Image(leftIconBytes).FitArea();
                                }
                            });

                            header.ConstantItem(titleChordWidth).AlignMiddle()
                                .Element(e => RenderArchedBrand(
                                    e,
                                    banner,
                                    titleFontId,
                                    titleSize,
                                    titleColor,
                                    titleChordWidth,
                                    titleBandHeight,
                                    arcAmplitude));

                            header.ConstantItem(headerIconWidth).AlignMiddle().AlignLeft()
                                .TranslateX(-headerIconOverlap).Element(e =>
                            {
                                if (rightIconBytes is { Length: > 0 })
                                {
                                    e.Height(headerIconHeight).Image(rightIconBytes).FitArea();
                                }
                            });

                            header.RelativeItem();
                        });

                        // Gold under-title flourish (approximate snowflake-line from reference).
                        headerCol.Item().Height(flourishHeight).AlignCenter().AlignMiddle().Row(fl =>
                        {
                            fl.RelativeItem().AlignMiddle()
                                .LineHorizontal(0.8f).LineColor(gold);
                            fl.ConstantItem(14).AlignCenter().AlignMiddle()
                                .Text("✦").FontSize(8).FontColor(gold);
                            fl.RelativeItem().AlignMiddle()
                                .LineHorizontal(0.8f).LineColor(gold);
                        });
                    });

                    const float contentTopPad = 2f;
                    const float gap = 4f;
                    var margin = 14f;
                    // Leave room for page-number footer and/or decorative spruce band on page bg.
                    var footerEstimate = showPageNumbers ? 16f : 6f;
                    if (pageBgBytes is { Length: > 0 })
                    {
                        footerEstimate = Math.Max(footerEstimate, 88f);
                    }

                    var available = PageSizes.A4.Height - (2 * margin) - docHeaderHeight - footerEstimate - contentTopPad;
                    var gapsTotal = Math.Max(0, pageCards.Count - 1) * gap;
                    var plateHeight = (available - gapsTotal) / Math.Max(1, pageCards.Count);
                    // Header chrome must match RenderPlate exactly — overflow here drops the grid.
                    var plateHeaderH = PlateHeaderHeight(plateScale, showBarcode);
                    var chrome = plateHeaderH + 2f;
                    var cellHeight = Math.Max(10f, (plateHeight - chrome) / BingoPlateGenerator.Rows);
                    var fillScale = plateScale with { CellMinHeight = cellHeight };

                    page.Content().PaddingTop(contentTopPad).Column(col =>
                    {
                        for (var i = 0; i < pageCards.Count; i++)
                        {
                            col.Item().Height(plateHeight).Element(e => RenderPlate(
                                e,
                                pageCards[i],
                                banner,
                                festiveRed,
                                emptyCell,
                                ink,
                                fillScale,
                                showBarcode));

                            if (i < pageCards.Count - 1)
                            {
                                col.Item().Height(gap);
                            }
                        }
                    });

                    if (showPageNumbers)
                    {
                        page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1)).Text(text =>
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
    /// Editable brand along a gentle upward arc (center high) so it sits over the snow-wave band.
    /// Per-glyph Rotate + Translate — not a baked image.
    /// </summary>
    private static void RenderArchedBrand(
        IContainer container,
        string text,
        string fontId,
        float fontSize,
        Color color,
        float width,
        float height,
        float amplitude)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var chars = text.ToCharArray();
        var n = chars.Length;
        var charStep = width / Math.Max(1, n);
        var maxTilt = Math.Clamp(n * 1.1f, 8f, 18f); // degrees from center to end

        // Tight Height box so Layers cannot inflate the page header and starve plate content.
        container.Height(height).Layers(layers =>
        {
            layers.PrimaryLayer();

            for (var i = 0; i < n; i++)
            {
                var t = n == 1 ? 0.5f : i / (float)(n - 1);
                // Rainbow arch: center highest (smaller Y), ends lower on the page.
                var y = amplitude * (1f - 4f * t * (1f - t));
                var tilt = -maxTilt + t * (2f * maxTilt);
                var x = i * charStep;

                layers.Layer()
                    .Unconstrained()
                    .TranslateX(x)
                    .TranslateY(y)
                    .Width(charStep + 2f)
                    .Height(fontSize + 6f)
                    .AlignCenter()
                    .AlignMiddle()
                    .Rotate(tilt)
                    .Text(chars[i].ToString())
                    .FontFamily(fontId)
                    .FontSize(fontSize)
                    .SemiBold()
                    .FontColor(color);
            }
        });
    }

    /// <summary>
    /// Reference plate: dark-red header bar with white brand + barcode box; white number cells;
    /// deep festive-red empty cells.
    /// </summary>
    private static float PlateHeaderHeight(PlateScale scale, bool showBarcode) =>
        Math.Max(scale.BannerFont + 6f, SeriesMarkerHeight(scale, showBarcode) + 4f);

    /// <summary>
    /// Reference plate: dark-red header bar with white brand + barcode inset; white number cells;
    /// deep festive-red empty cells. Header height must fit inside parent Height(plateHeight)
    /// or QuestPDF silently drops the grid.
    /// </summary>
    private static void RenderPlate(
        IContainer container,
        BingoCard card,
        string banner,
        Color festiveRed,
        Color emptyCell,
        Color ink,
        PlateScale scale,
        bool showBarcode)
    {
        var grid = BingoPlateGenerator.FromJson(card.CellsJson);
        var seriesText = BingoPlateGenerator.FormatSeries(card.SeriesNumber);
        var hriFont = HriFontSize(scale);
        var hriLine = HriLineHeight(scale);
        var headerH = PlateHeaderHeight(scale, showBarcode);
        byte[]? barcodePng = null;
        if (showBarcode)
        {
            barcodePng = BingoSeriesBarcode.CreateCode128Png(
                seriesText,
                widthPx: (int)(scale.BarcodeWidth * 3f),
                heightPx: (int)(scale.BarcodeHeight * 3f));
        }

        container
            .Border(1.2f)
            .BorderColor(festiveRed)
            .Column(col =>
        {
            // Dark red header — no vertical padding (would overflow Height and drop the grid).
            col.Item().Height(headerH).Background(festiveRed).PaddingHorizontal(6).Row(r =>
            {
                r.RelativeItem().AlignMiddle()
                    .Text(banner)
                    .FontSize(scale.BannerFont)
                    .SemiBold()
                    .FontColor(Colors.White);

                r.ConstantItem(scale.BarcodeWidth + 6f).AlignMiddle().AlignRight()
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
        _ => new PlateScale(0, 11, 11, 0, 0, 0, 90, 16) // 6
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
    public string? BackgroundColor { get; init; }
    public string? TitleFont { get; init; }
    public int? TitleSize { get; init; }
    public bool? ShowBarcode { get; init; }
    public bool? ShowPageNumbers { get; init; }
}
