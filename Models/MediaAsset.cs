namespace Julebanko.Models;

public class MediaAsset
{
    public int Id { get; set; }
    public MediaSlot Slot { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int? UploadedByAdminId { get; set; }
    public AdminUser? UploadedByAdmin { get; set; }
}
