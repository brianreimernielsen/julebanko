using Julebanko.Data;
using Julebanko.Models;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Services;

public class MediaService(AppDbContext db, IWebHostEnvironment env)
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif"
        };

    public const long MaxBytes = 2 * 1024 * 1024;

    public async Task<IReadOnlyDictionary<MediaSlot, MediaAsset>> GetAllAsync()
    {
        var items = await db.MediaAssets.AsNoTracking().ToListAsync();
        return items.ToDictionary(m => m.Slot);
    }

    public async Task<(bool Ok, string? Error)> UploadAsync(MediaSlot slot, IFormFile file, int? adminId)
    {
        if (file.Length <= 0)
        {
            return (false, "EmptyFile");
        }

        if (file.Length > MaxBytes)
        {
            return (false, "FileTooLarge");
        }

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext) || !AllowedContentTypes.Contains(file.ContentType))
        {
            return (false, "InvalidType");
        }

        var uploadsRoot = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(uploadsRoot);

        var safeName = $"{slot.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var physicalPath = Path.Combine(uploadsRoot, safeName);
        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream);
        }

        var relative = $"/uploads/{safeName}";
        var existing = await db.MediaAssets.FirstOrDefaultAsync(m => m.Slot == slot);
        if (existing is not null)
        {
            TryDeleteFile(existing.RelativePath);
            existing.FileName = file.FileName;
            existing.ContentType = file.ContentType;
            existing.RelativePath = relative;
            existing.UploadedAt = DateTime.UtcNow;
            existing.UploadedByAdminId = adminId;
        }
        else
        {
            db.MediaAssets.Add(new MediaAsset
            {
                Slot = slot,
                FileName = file.FileName,
                ContentType = file.ContentType,
                RelativePath = relative,
                UploadedAt = DateTime.UtcNow,
                UploadedByAdminId = adminId
            });
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(MediaSlot slot)
    {
        var existing = await db.MediaAssets.FirstOrDefaultAsync(m => m.Slot == slot);
        if (existing is null)
        {
            return (false, "NotFound");
        }

        TryDeleteFile(existing.RelativePath);
        db.MediaAssets.Remove(existing);
        await db.SaveChangesAsync();
        return (true, null);
    }

    private void TryDeleteFile(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var path = Path.Combine(env.ContentRootPath, "App_Data", "uploads", name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
