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
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await draws.DrawAsync(Count);
        AllNumbers = await draws.GetAllAsync();
        if (!result.Ok)
        {
            Error = result.Error;
            return Page();
        }

        LastDrawn = result.Numbers;
        Success = "ok";
        return Page();
    }
}
