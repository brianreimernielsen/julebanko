using Julebanko.Data;
using Julebanko.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Julebanko.Services;

public class BingoPdfService(SettingsService settings)
{
    static BingoPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> BuildPdfAsync(IReadOnlyList<BingoCard> cards, int platesPerPage, string? batchBanner)
    {
        platesPerPage = platesPerPage is 1 or 2 or 3 or 4 or 6 or 8 or 9 ? platesPerPage : 6;

        var brand = await settings.GetBrandNameOrFallbackAsync("da");
        var banner = !string.IsNullOrWhiteSpace(batchBanner)
            ? batchBanner.Trim()
            : await settings.GetAsync(SettingKeys.BingoBannerText, "");
        if (string.IsNullOrWhiteSpace(banner))
        {
            banner = brand;
        }

        var primary = await settings.GetAsync(SettingKeys.BingoPrimaryColor, "");
        if (string.IsNullOrWhiteSpace(primary))
        {
            primary = await settings.GetAsync(SettingKeys.PlayerBgColor, "#8B1E1E");
        }

        var accent = await settings.GetAsync(SettingKeys.BingoAccentColor, "");
        if (string.IsNullOrWhiteSpace(accent))
        {
            accent = await settings.GetAsync(SettingKeys.AccentColor, "#D4A017");
        }

        var festiveRaw = await settings.GetAsync(SettingKeys.BingoFestiveLook, "true");
        var festive = !string.Equals(festiveRaw, "false", StringComparison.OrdinalIgnoreCase) && festiveRaw != "0";

        var primaryColor = ParseColor(primary, Colors.Red.Darken3);
        var accentColor = ParseColor(accent, Colors.Amber.Darken2);
        var paper = festive ? Color.FromHex("#FFF8F0") : Colors.White;
        var ink = Color.FromHex("#1A1A1A");

        var layouts = LayoutFor(platesPerPage);
        var pages = cards
            .Select((card, i) => (card, i))
            .GroupBy(x => x.i / platesPerPage)
            .Select(g => g.Select(x => x.card).ToList())
            .ToList();

        var document = Document.Create(container =>
        {
            foreach (var pageCards in pages)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(18);
                    page.PageColor(paper);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(ink));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text(banner)
                            .FontSize(festive ? 18 : 16)
                            .SemiBold()
                            .FontColor(primaryColor);
                        if (festive)
                        {
                            col.Item().PaddingTop(2).AlignCenter().Text("Bankoplader")
                                .FontSize(9)
                                .FontColor(accentColor);
                        }
                    });

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
                                            RenderPlate(e, pageCards[idx], banner, primaryColor, accentColor, festive, ink);
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
        Color primary,
        Color accent,
        bool festive,
        Color ink)
    {
        var grid = BingoPlateGenerator.FromJson(card.CellsJson);
        var series = BingoPlateGenerator.FormatSeries(card.SeriesNumber);

        container.Border(1.2f).BorderColor(primary).Background(Colors.White).Padding(6).Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(banner).FontSize(9).SemiBold().FontColor(primary);
                r.ConstantItem(70).AlignRight().Text($"Nr. {series}")
                    .FontSize(9).SemiBold().FontColor(accent);
            });

            if (festive)
            {
                col.Item().PaddingVertical(3).LineHorizontal(0.8f).LineColor(accent);
            }

            col.Item().PaddingTop(4).Table(table =>
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
                        table.Cell().Border(0.6f).BorderColor(Colors.Grey.Darken1)
                            .Background(n is null ? Color.FromHex("#F3F3F3") : Colors.White)
                            .MinHeight(22)
                            .AlignMiddle()
                            .AlignCenter()
                            .Text(n?.ToString() ?? "")
                            .FontSize(11)
                            .SemiBold()
                            .FontColor(ink);
                    }
                }
            });
        });
    }

    private static (int Rows, int Cols) LayoutFor(int platesPerPage) => platesPerPage switch
    {
        1 => (1, 1),
        2 => (2, 1),
        3 => (3, 1),
        4 => (2, 2),
        6 => (3, 2),
        8 => (4, 2),
        9 => (3, 3),
        _ => (3, 2)
    };

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            return Color.FromHex(hex.Trim());
        }
        catch
        {
            return fallback;
        }
    }
}
