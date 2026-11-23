namespace Julebanko.Models;

public class DrawnNumber
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;
    public int Number { get; set; }
    public DateTime DrawnAt { get; set; } = DateTime.UtcNow;
    public Guid DrawBatchId { get; set; }
}
