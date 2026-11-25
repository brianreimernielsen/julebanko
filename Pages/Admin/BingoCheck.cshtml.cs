using System.Globalization;
using Julebanko.Models;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin;

public class BingoCheckModel(BingoCardService bingo, ClaimService claims, DrawService draws) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? SeriesQuery { get; set; }

    public IReadOnlyList<BingoCardListItem> Cards { get; private set; } = Array.Empty<BingoCardListItem>();
    public int TotalCards { get; private set; }
    public IReadOnlyList<Claim> RelatedClaims { get; private set; } = Array.Empty<Claim>();
    public bool HasExactSeriesQuery { get; private set; }
    public HashSet<int> DrawnSet { get; private set; } = new();

    public async Task OnGetAsync()
    {
        DrawnSet = (await draws.GetAllAsync()).Select(d => d.Number).ToHashSet();

        // Exact series lookup shows a focused result; blank query lists a sample of season plates.
        var take = string.IsNullOrWhiteSpace(SeriesQuery) ? 50 : 20;
        var (items, total) = await bingo.SearchCardsAsync(SeriesQuery, 0, take);
        Cards = items;
        TotalCards = total;
        HasExactSeriesQuery = BingoPlateGenerator.TryParseSeries(SeriesQuery, out _);

        RelatedClaims = HasExactSeriesQuery
            ? await claims.GetBySeriesAsync(SeriesQuery!)
            : Array.Empty<Claim>();
    }

    public string WinLabel(WinType type) =>
        ClaimService.WinTypeLabel(type, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public string ValidationLabel(ClaimValidationStatus status) =>
        ClaimService.ValidationLabel(status, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public string ClaimStatusLabel(ClaimStatus status)
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var da = culture.StartsWith("da", StringComparison.OrdinalIgnoreCase);
        var sv = culture.StartsWith("sv", StringComparison.OrdinalIgnoreCase);
        return status switch
        {
            ClaimStatus.Pending => da ? "Afventer" : sv ? "Väntar" : "Pending",
            ClaimStatus.Approved => da ? "Godkendt" : sv ? "Godkänd" : "Approved",
            ClaimStatus.Rejected => da ? "Afvist" : sv ? "Avvisad" : "Rejected",
            _ => status.ToString()
        };
    }
}
