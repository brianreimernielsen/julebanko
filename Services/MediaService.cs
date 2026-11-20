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

    /// <summary>
    /// Bundled samples under wwwroot/sample-media that Setup Admin may reset.
    /// Night landscape is locked (no PlayerBackground) — red top uses Theme color;
    /// hero band / logo remain customizable.
    /// </summary>
    public static readonly (MediaSlot Slot, string FileName, string ContentType)[] SampleSlots =
    [
        (MediaSlot.PlayerHero, "hero.jpg", "image/jpeg"),
        (MediaSlot.PlayerHeroLogo, "hero-logo.png", "image/png"),
        (MediaSlot.PlayerDecoration, "decoration.jpg", "image/jpeg")
    ];

    public const long MaxBytes = 2 * 1024 * 1024;

    public async Task<IReadOnlyDictionary<MediaSlot, MediaAsset>> GetAllAsync()
    {
        var items = await db.MediaAssets.AsNoTracking().ToListAsync();
        return items.ToDictionary(m => m.Slot);
    }

    /// <summary>Slots Setup Admin may upload. Night landscape (PlayerBackground) is locked.</summary>
    private static readonly HashSet<MediaSlot> UploadableSlots =
    [
        MediaSlot.PlayerHero,
        MediaSlot.PlayerHeroLogo,
        MediaSlot.PlayerDecoration
    ];

    public async Task<(bool Ok, string? Error)> UploadAsync(MediaSlot slot, IFormFile file, int? adminId)
    {
        if (!UploadableSlots.Contains(slot))
        {
            return (false, "SlotLocked");
        }

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

    /// <summary>
    /// Restore customizable player media from wwwroot/sample-media (hero, logo, decoration).
    /// Clears any legacy PlayerBackground upload — night landscape stays locked in CSS/nisse-stage.
    /// </summary>
    public async Task<(bool Ok, string? Error, int Restored)> ResetToSampleDefaultsAsync(int? adminId = null)
    {
        var sampleRoot = Path.Combine(env.WebRootPath, "sample-media");
        var uploadRoot = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(uploadRoot);

        // Drop legacy full-bleed background so it cannot reappear if UI is re-enabled.
        var legacyBg = await db.MediaAssets.FirstOrDefaultAsync(m => m.Slot == MediaSlot.PlayerBackground);
        if (legacyBg is not null)
        {
            TryDeleteFile(legacyBg.RelativePath);
            db.MediaAssets.Remove(legacyBg);
            await db.SaveChangesAsync();
        }

        var restored = 0;
        foreach (var (slot, fileName, contentType) in SampleSlots)
        {
            var source = Path.Combine(sampleRoot, fileName);
            if (!File.Exists(source))
            {
                return (false, $"SampleMissing:{fileName}", restored);
            }

            var destName = $"{slot.ToString().ToLowerInvariant()}-sample{Path.GetExtension(fileName)}";
            var destPath = Path.Combine(uploadRoot, destName);
            var relative = $"/uploads/{destName}";

            var existing = await db.MediaAssets.FirstOrDefaultAsync(m => m.Slot == slot);
            if (existing is not null)
            {
                var oldName = Path.GetFileName(existing.RelativePath);
                if (!string.Equals(oldName, destName, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(existing.RelativePath);
                }

                File.Copy(source, destPath, overwrite: true);
                existing.FileName = fileName;
                existing.ContentType = contentType;
                existing.RelativePath = relative;
                existing.UploadedAt = DateTime.UtcNow;
                existing.UploadedByAdminId = adminId;
            }
            else
            {
                File.Copy(source, destPath, overwrite: true);
                db.MediaAssets.Add(new MediaAsset
                {
                    Slot = slot,
                    FileName = fileName,
                    ContentType = contentType,
                    RelativePath = relative,
                    UploadedAt = DateTime.UtcNow,
                    UploadedByAdminId = adminId
                });
            }

            restored++;
        }

        await db.SaveChangesAsync();
        return (true, null, restored);
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
