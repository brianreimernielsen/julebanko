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

    /// <summary>Player-entered plate series (digits; leading zeros ignored when parsing).</summary>
    public string? SeriesNumber { get; set; }

    public int? BingoCardId { get; set; }
    public BingoCard? BingoCard { get; set; }

    public ClaimValidationStatus ValidationStatus { get; set; } = ClaimValidationStatus.NotChecked;

    /// <summary>True when auto-validate ran and the plate matched the claimed win type.</summary>
    public bool AutoValidated { get; set; }

    /// <summary>Short note for admin/email (e.g. which rows hit, or why mismatch).</summary>
    public string? ValidationNote { get; set; }
}
