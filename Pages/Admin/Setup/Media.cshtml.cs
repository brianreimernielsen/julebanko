using Julebanko.Models;
using Julebanko.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Julebanko.Pages.Admin.Setup;

public class MediaModel(MediaService media) : PageModel
{
    public IReadOnlyDictionary<MediaSlot, MediaAsset> Assets { get; private set; } =
        new Dictionary<MediaSlot, MediaAsset>();
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync()
    {
        Assets = await media.GetAllAsync();
    }

    public async Task<IActionResult> OnPostUploadAsync(MediaSlot slot, IFormFile? file)
    {
        if (file is null)
        {
            Message = "No file";
            IsError = true;
            Assets = await media.GetAllAsync();
            return Page();
        }

        var adminId = AdminAuthService.GetUserId(User);
        var result = await media.UploadAsync(slot, file, adminId);
        Message = result.Ok ? "ok" : result.Error;
        IsError = !result.Ok;
        Assets = await media.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(MediaSlot slot)
    {
        var result = await media.DeleteAsync(slot);
        Message = result.Ok ? "deleted" : result.Error;
        IsError = !result.Ok;
        Assets = await media.GetAllAsync();
        return Page();
    }
}
