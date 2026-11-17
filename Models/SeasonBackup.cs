namespace Julebanko.Models;

public class SeasonBackup
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByAdminId { get; set; }
    public AdminUser CreatedByAdmin { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public bool IsRestored { get; set; }
}
