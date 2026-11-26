namespace Julebanko.Models;

/// <summary>
/// One Danish banko plate (3×9 grid, 15 numbers). CellsJson is a flat 27-length array of nullable ints (row-major).
/// </summary>
public class BingoCard
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public BingoCardBatch Batch { get; set; } = null!;
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    /// <summary>Season-scoped unique series number (displayed zero-padded).</summary>
    public int SeriesNumber { get; set; }

    /// <summary>JSON: int?[27] row-major (3 rows × 9 cols).</summary>
    public string CellsJson { get; set; } = "[]";

    /// <summary>Sorted 15 numbers joined, for uniqueness checks within a season.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
