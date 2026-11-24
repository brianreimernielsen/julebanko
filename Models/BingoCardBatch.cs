namespace Julebanko.Models;

public class BingoCardBatch
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByAdminId { get; set; }
    public AdminUser CreatedByAdmin { get; set; } = null!;
    public int PlateCount { get; set; }
    public int PlatesPerPage { get; set; } = 6;
    public string? BannerText { get; set; }
    public string? Note { get; set; }

    public ICollection<BingoCard> Cards { get; set; } = new List<BingoCard>();
}
