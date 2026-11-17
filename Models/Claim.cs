namespace Julebanko.Models;

public class Claim
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;
    public string PlayerName { get; set; } = string.Empty;
    public WinType WinType { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public AdminUser? ReviewedByAdmin { get; set; }
}
