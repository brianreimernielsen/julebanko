using Julebanko.Data;
using Julebanko.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Julebanko.Services;

public class BingoPdfService(SettingsService settings, MediaService media)
{
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
        platesPerPage = platesPerPage is 1 or 2 or 3 or 4 or 6 or 8 or 9 ? platesPerPage : 6;

        // 1–6: larger cells so plates fill A4 width/height (less empty bottom). >6: compact current sizing.
        var fillPage = platesPerPage <= 6;

        var brand = await settings.GetBrandNameOrFallbackAsync("da");
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
        // Series color kept for barcode/HRI contrast contexts; gold under-header rule removed (Brian).
        _ = ParseColor(scheme.ColorB, Colors.Amber.Darken2);

        var frameHex = !string.IsNullOrWhiteSpace(styleOverrides?.FrameColor)
            ? styleOverrides!.FrameColor!
            : await settings.GetAsync(SettingKeys.BingoFrameColor, "");
        if (string.IsNullOrWhiteSpace(frameHex))
        {
            // Legacy fallback: BingoPrimaryColor → scheme ColorA
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
            : await settings.GetAsync(SettingKeys.BingoPdfBackgroundColor, "#FFFFFF");
        if (string.IsNullOrWhiteSpace(bgHex))
        {
            bgHex = "#FFFFFF";
        }

        var showBarcode = styleOverrides?.ShowBarcode
            ?? IsTruthy(await settings.GetAsync(SettingKeys.BingoShowBarcode, "true"));

        // Default OFF — Brian questioned usefulness; opt-in via Design «Vis sidenumre».
        var showPageNumbers = styleOverrides?.ShowPageNumbers
            ?? IsTruthy(await settings.GetAsync(SettingKeys.BingoShowPageNumbers, "false"));

        var frameColor = ParseColor(frameHex, ParseColor(scheme.ColorA, Colors.Red.Darken3));
        var bannerColor = ParseColor(bannerHex, frameColor);
        var paper = ParseColor(bgHex, Colors.White);
        var ink = Color.FromHex("#1A1A1A");
        var plateFill = BlendTowardWhite(scheme.ColorA, 0.88f);
        var emptyCell = BlendTowardWhite(scheme.ColorA, 0.75f);

        // Header garlands: uploaded override, else bundled sample-media defaults.
        // Full-page BingoPdfBackground image is intentionally not drawn (Brian: prefer header icons).
        var leftIcon = await media.TryReadBingoHeaderIconAsync(MediaSlot.BingoHeaderIconLeft);
        var rightIcon = await media.TryReadBingoHeaderIconAsync(MediaSlot.BingoHeaderIconRight);
        byte[]? leftIconBytes = leftIcon?.Bytes;
        byte[]? rightIconBytes = rightIcon?.Bytes;

        var layouts = LayoutFor(platesPerPage);
        var pages = cards
            .Select((card, i) => (card, i))
            .GroupBy(x => x.i / platesPerPage)
            .Select(g => g.Select(x => x.card).ToList())
            .ToList();

        var plateScale = PlateScaleFor(platesPerPage, fillPage);
        // Document header: garlands + title. Icon height tracks title size.
        var headerIconHeight = Math.Max(22f, titleSize + 4f);
        var headerIconWidth = headerIconHeight * 3f; // assets are ~3:1
        var docHeaderHeight = Math.Max(titleSize + 12f, headerIconHeight + 4f);

        var document = Document.Create(container =>
        {
            foreach (var pageCards in pages)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(fillPage ? 12 : 18);
                    page.PageColor(paper);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(ink));

                    page.Header().Height(docHeaderHeight).Row(header =>
                    {
                        header.ConstantItem(headerIconWidth).AlignMiddle().AlignLeft().Element(e =>
                        {
                            if (leftIconBytes is { Length: > 0 })
                            {
                                e.Height(headerIconHeight).Image(leftIconBytes).FitArea();
                            }
                        });

                        header.RelativeItem().AlignCenter().AlignMiddle().Text(banner)
                            .FontFamily(titleFontId)
                            .FontSize(titleSize)
                            .SemiBold()
                            .FontColor(bannerColor);

                        header.ConstantItem(headerIconWidth).AlignMiddle().AlignRight().Element(e =>
                        {
                            if (rightIconBytes is { Length: > 0 })
                            {
                                e.Height(headerIconHeight).Image(rightIconBytes).FitArea();
                            }
                        });
                    });

                    if (fillPage)
                    {
                        // ≤6: single full-width column; equal heights under header/footer (no ExtendVertical —
                        // nested extend overflows and QuestPDF paginates mid-plate).
                        const float contentTopPad = 4f;
                        const float gap = 3f;
                        var margin = 12f;
                        var footerEstimate = showPageNumbers ? 18f : 4f;
                        var available = PageSizes.A4.Height - (2 * margin) - docHeaderHeight - footerEstimate - contentTopPad;
                        var gapsTotal = Math.Max(0, pageCards.Count - 1) * gap;
                        var plateHeight = (available - gapsTotal) / Math.Max(1, pageCards.Count);
                        // Compact header: brand + optional Code128 / series digits (grid stays primary).
                        // No gold horizontal rule under plate header (Brian).
                        var headerRow = Math.Max(
                            plateScale.BannerFont + 8f,
                            SeriesMarkerHeight(plateScale, showBarcode));
                        var chrome = (plateScale.Padding * 2) + headerRow
                            + plateScale.TablePad + 4f; // +4 safety vs overflow
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
                                    frameColor,
                                    bannerColor,
                                    plateFill,
                                    emptyCell,
                                    ink,
                                    fillScale,
                                    fillHeight: true,
                                    showBarcode));

                                if (i < pageCards.Count - 1)
                                {
                                    col.Item().Height(gap);
                                }
                            }
                        });
                    }
                    else
                    {
                        page.Content().PaddingTop(8).Column(col =>
                        {
                            for (var row = 0; row < layouts.Rows; row++)
                            {
                                col.Item().PaddingBottom(10).Row(r =>
                                {
                                    for (var c = 0; c < layouts.Cols; c++)
                                    {
                                        var idx = row * layouts.Cols + c;
                                        r.RelativeItem().Padding(4).Element(e =>
                                        {
                                            if (idx < pageCards.Count)
                                            {
                                                RenderPlate(
                                                    e,
                                                    pageCards[idx],
                                                    banner,
                                                    frameColor,
                                                    bannerColor,
                                                    plateFill,
                                                    emptyCell,
                                                    ink,
                                                    plateScale,
                                                    fillHeight: false,
                                                    showBarcode);
                                            }
                                            else
                                            {
                                                e.Height(1);
                                            }
                                        });
                                    }
                                });
                            }
                        });
                    }

                    if (showPageNumbers)
                    {
                        // Footer only when opted in: «Brand 1/4» — not on plates.
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

    private static void RenderPlate(
        IContainer container,
        BingoCard card,
        string banner,
        Color frame,
        Color bannerText,
        Color plateFill,
        Color emptyCell,
        Color ink,
        PlateScale scale,
        bool fillHeight,
        bool showBarcode)
    {
        var grid = BingoPlateGenerator.FromJson(card.CellsJson);
        var seriesText = BingoPlateGenerator.FormatSeries(card.SeriesNumber);
        var hriFont = HriFontSize(scale);
        var hriLine = HriLineHeight(scale);
        var markerHeight = SeriesMarkerHeight(scale, showBarcode);
        byte[]? barcodePng = null;
        if (showBarcode)
        {
            // Short Code128 — same zero-padded digits USB wedge → Tjek; HRI drawn under bars in black.
            barcodePng = BingoSeriesBarcode.CreateCode128Png(
                seriesText,
                widthPx: (int)(scale.BarcodeWidth * 3f),
                heightPx: (int)(scale.BarcodeHeight * 3f));
        }

        // fillHeight: parent already set equal Height(plateHeight); keep content ≤ that (no ExtendVertical).
        _ = fillHeight;

        container
            .Border(1.4f)
            .BorderColor(frame)
            .Background(plateFill)
            .Padding(scale.Padding)
            .Column(col =>
        {
            col.Item().Height(Math.Max(scale.BannerFont + 4f, markerHeight)).Row(r =>
            {
                r.RelativeItem().AlignMiddle()
                    .Text(banner).FontSize(scale.BannerFont).SemiBold().FontColor(bannerText);
                // Right field: optional Code128 + centered black series digits (or number-only).
                r.ConstantItem(scale.BarcodeWidth + 4f).PaddingLeft(4f).Column(bc =>
                {
                    if (showBarcode && barcodePng is not null)
                    {
                        // FitHeight + AlignCenter: no wide white slab; HRI sits under the bars.
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

            // Gold/yellow horizontal rule under plate header removed (Brian: redundant).

            col.Item().PaddingTop(scale.TablePad).Table(table =>
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
                        // Grid number face/size stay fixed (Brian: clear & not configurable).
                        table.Cell()
                            .Border(0.6f)
                            .BorderColor(Colors.Grey.Darken1)
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

    /// <summary>Mix hex color toward white by amount (0–1) for readable plate fills.</summary>
    private static Color BlendTowardWhite(string hex, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        if (!ColorHex.TryNormalize(hex, out var n))
        {
            return Colors.White;
        }

        var rgb = ColorHex.ToRgb(n)!.Value;
        var r = (byte)(rgb.R + (255 - rgb.R) * amount);
        var g = (byte)(rgb.G + (255 - rgb.G) * amount);
        var b = (byte)(rgb.B + (255 - rgb.B) * amount);
        return Color.FromRGB(r, g, b);
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

    /// <summary>HRI / number-only — compact so header only grows by one number line.</summary>
    private static float HriFontSize(PlateScale scale) => Math.Max(7f, scale.BannerFont - 4f);

    /// <summary>Line box for series digits (font + small pad so QuestPDF does not clip/overflow).</summary>
    private static float HriLineHeight(PlateScale scale) => HriFontSize(scale) + 3f;

    /// <summary>Optional Code128 (~16pt / ~5.6mm at 6/page) + series digits — grid stays primary.</summary>
    private static float SeriesMarkerHeight(PlateScale scale, bool showBarcode) =>
        showBarcode ? scale.BarcodeHeight + HriLineHeight(scale) : HriLineHeight(scale);

    private static bool IsTruthy(string? raw) =>
        !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) && raw != "0";

    private static PlateScale PlateScaleFor(int platesPerPage, bool fillPage)
    {
        if (!fillPage)
        {
            // Compact (>6): low Code128; header grows only for HRI under bars.
            return new PlateScale(5, 10, 11, 22, 3, 2, 72, 12);
        }

        // ≤6 single-column fillHeight; CellMinHeight set per page.
        // 6/page Code128 height 16pt ≈ 5.6mm (scannable; not header-dominant).
        // CellFont stays locked at these sizes (not Setup-configurable).
        return platesPerPage switch
        {
            1 => new PlateScale(10, 18, 22, 0, 8, 5, 120, 22),
            2 => new PlateScale(8, 16, 18, 0, 6, 4, 110, 20),
            3 => new PlateScale(6, 14, 15, 0, 4, 3, 100, 18),
            4 => new PlateScale(5, 13, 13, 0, 3, 2, 95, 16),
            _ => new PlateScale(3, 12, 11, 0, 1, 1, 90, 16) // 6 — ~5.6mm Code128 + centered HRI
        };
    }

    /// <summary>≤6 → single column (N×1). &gt;6 → compact multi-column grid.</summary>
    private static (int Rows, int Cols) LayoutFor(int platesPerPage) => platesPerPage switch
    {
        1 => (1, 1),
        2 => (2, 1),
        3 => (3, 1),
        4 => (4, 1),
        6 => (6, 1),
        8 => (4, 2),
        9 => (3, 3),
        _ => (6, 1)
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
    /// <summary>When set, overrides <see cref="SettingKeys.BingoShowBarcode"/> for preview.</summary>
    public bool? ShowBarcode { get; init; }
    /// <summary>When set, overrides <see cref="SettingKeys.BingoShowPageNumbers"/> for preview.</summary>
    public bool? ShowPageNumbers { get; init; }
}
