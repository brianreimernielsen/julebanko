namespace Julebanko.Models;

public class Season
{
    public int Id { get; set; }
    public int Year { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DrawnNumber> DrawnNumbers { get; set; } = new List<DrawnNumber>();
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public ICollection<BingoCardBatch> BingoCardBatches { get; set; } = new List<BingoCardBatch>();
    public ICollection<BingoCard> BingoCards { get; set; } = new List<BingoCard>();
}
