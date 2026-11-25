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
    }

    public async Task<byte[]> BuildPdfAsync(IReadOnlyList<BingoCard> cards, int platesPerPage, string? batchBanner)
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

        var schemeId = await settings.GetAsync(SettingKeys.BingoPlateScheme, BingoPlateSchemes.DefaultId);
        var scheme = BingoPlateSchemes.GetOrDefault(schemeId);
        var seriesColor = ParseColor(scheme.ColorB, Colors.Amber.Darken2);

        var frameHex = await settings.GetAsync(SettingKeys.BingoFrameColor, "");
        if (string.IsNullOrWhiteSpace(frameHex))
        {
            // Legacy fallback: BingoPrimaryColor → scheme ColorA
            frameHex = await settings.GetAsync(SettingKeys.BingoPrimaryColor, "");
            if (string.IsNullOrWhiteSpace(frameHex))
            {
                frameHex = scheme.ColorA;
            }
        }

        var bannerHex = await settings.GetAsync(SettingKeys.BingoBannerColor, "");
        if (string.IsNullOrWhiteSpace(bannerHex))
        {
            bannerHex = frameHex;
        }

        var bgHex = await settings.GetAsync(SettingKeys.BingoPdfBackgroundColor, "#FFFFFF");
        if (string.IsNullOrWhiteSpace(bgHex))
        {
            bgHex = "#FFFFFF";
        }

        var frameColor = ParseColor(frameHex, ParseColor(scheme.ColorA, Colors.Red.Darken3));
        var bannerColor = ParseColor(bannerHex, frameColor);
        var paper = ParseColor(bgHex, Colors.White);
        var ink = Color.FromHex("#1A1A1A");
        var plateFill = BlendTowardWhite(scheme.ColorA, 0.88f);
        var emptyCell = BlendTowardWhite(scheme.ColorA, 0.75f);

        byte[]? bgImage = null;
        var bgAsset = await media.TryReadBytesAsync(MediaSlot.BingoPdfBackground);
        if (bgAsset is not null)
        {
            bgImage = bgAsset.Value.Bytes;
        }

        var layouts = LayoutFor(platesPerPage);
        var pages = cards
            .Select((card, i) => (card, i))
            .GroupBy(x => x.i / platesPerPage)
            .Select(g => g.Select(x => x.card).ToList())
            .ToList();

        var plateScale = PlateScaleFor(platesPerPage, fillPage);

        var document = Document.Create(container =>
        {
            foreach (var pageCards in pages)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(fillPage ? 14 : 18);
                    page.PageColor(paper);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(ink));

                    if (bgImage is { Length: > 0 })
                    {
                        page.Background().Image(bgImage).FitUnproportionally();
                    }

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text(banner)
                            .FontSize(16)
                            .SemiBold()
                            .FontColor(bannerColor);
                    });

                    if (fillPage)
                    {
                        // ≤6: single full-width column; equal heights under header/footer (no ExtendVertical —
                        // nested extend overflows and QuestPDF paginates mid-plate).
                        const float contentTopPad = 6f;
                        const float gap = 4f;
                        var margin = 14f;
                        var headerEstimate = 28f;
                        var footerEstimate = 20f;
                        var available = PageSizes.A4.Height - (2 * margin) - headerEstimate - footerEstimate - contentTopPad;
                        var gapsTotal = Math.Max(0, pageCards.Count - 1) * gap;
                        var plateHeight = (available - gapsTotal) / Math.Max(1, pageCards.Count);
                        // Banner row + line + pads inside plate → leftover for 3 grid rows.
                        var chrome = (plateScale.Padding * 2) + plateScale.BannerFont + 6f
                            + (plateScale.LinePad * 2) + 2f + plateScale.TablePad + 4f; // +4 safety vs overflow
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
                                    seriesColor,
                                    ink,
                                    fillScale,
                                    fillHeight: true));

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
                                                    seriesColor,
                                                    ink,
                                                    plateScale,
                                                    fillHeight: false);
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

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span($"{brand} · ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
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
        Color series,
        Color ink,
        PlateScale scale,
        bool fillHeight)
    {
        var grid = BingoPlateGenerator.FromJson(card.CellsJson);
        var seriesText = BingoPlateGenerator.FormatSeries(card.SeriesNumber);

        // fillHeight: parent already set equal Height(plateHeight); keep content ≤ that (no ExtendVertical).
        _ = fillHeight;

        container
            .Border(1.4f)
            .BorderColor(frame)
            .Background(plateFill)
            .Padding(scale.Padding)
            .Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(banner).FontSize(scale.BannerFont).SemiBold().FontColor(bannerText);
                r.ConstantItem(scale.SeriesWidth).AlignRight().Text($"Nr. {seriesText}")
                    .FontSize(scale.BannerFont).SemiBold().FontColor(series);
            });

            col.Item().PaddingVertical(scale.LinePad).LineHorizontal(0.8f).LineColor(series);

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
        float SeriesWidth);

    private static PlateScale PlateScaleFor(int platesPerPage, bool fillPage)
    {
        if (!fillPage)
        {
            // Compact (>6): keep prior sizing so 8/9 still fit.
            return new PlateScale(6, 9, 11, 22, 4, 3, 70);
        }

        // ≤6 use single-column fillHeight layout; CellMinHeight unused there (ExtendVertical).
        // Fonts sized for readable full-width stacked plates (6 is tightest).
        return platesPerPage switch
        {
            1 => new PlateScale(10, 16, 22, 0, 8, 5, 110),
            2 => new PlateScale(8, 14, 18, 0, 6, 4, 100),
            3 => new PlateScale(6, 12, 15, 0, 4, 3, 90),
            4 => new PlateScale(5, 11, 13, 0, 3, 2, 85),
            _ => new PlateScale(4, 9, 11, 0, 2, 2, 75) // 6
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
