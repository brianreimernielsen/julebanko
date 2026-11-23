using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin;

public class DrawModel(DrawService draws) : PageModel
{
    [BindProperty]
    public int Count { get; set; } = 2;

    public IReadOnlyList<int> LastDrawn { get; private set; } = Array.Empty<int>();
    public IReadOnlyList<Models.DrawnNumber> AllNumbers { get; private set; } = Array.Empty<Models.DrawnNumber>();
    public string? Error { get; private set; }
    public string? Success { get; private set; }

    public async Task OnGetAsync()
    {
        AllNumbers = await draws.GetAllAsync();
        RestoreTempFeedback();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await draws.DrawAsync(Count);
        TempData["DrawCount"] = Count.ToString();

        if (!result.Ok)
        {
            TempData["DrawError"] = result.Error;
            // PRG: land on GET so auto-refresh / browser reload cannot re-POST the draw.
            return RedirectToPage();
        }

        TempData["DrawSuccess"] = "ok";
        TempData["LastDrawn"] = string.Join(",", result.Numbers);
        return RedirectToPage();
    }

    private void RestoreTempFeedback()
    {
        if (TempData["DrawError"] is string err && !string.IsNullOrWhiteSpace(err))
        {
            Error = err;
        }

        if (TempData["DrawSuccess"] is string)
        {
            Success = "ok";
        }

        if (TempData["LastDrawn"] is string csv && !string.IsNullOrWhiteSpace(csv))
        {
            LastDrawn = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var n) ? n : (int?)null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();
        }

        if (TempData["DrawCount"] is string countText && int.TryParse(countText, out var count) && count >= 1)
        {
            Count = count;
        }
    }
}
