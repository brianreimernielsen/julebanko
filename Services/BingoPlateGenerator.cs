using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Julebanko.Services;

/// <summary>
/// Danish banko: numbers 1–90, 9 columns (1–9, 10–19, …, 80–90), 3 rows × 5 numbers = 15.
/// </summary>
public static class BingoPlateGenerator
{
    public const int Rows = 3;
    public const int Cols = 9;
    public const int NumbersPerRow = 5;
    public const int NumbersPerPlate = 15;

    private static readonly JsonSerializerOptions JsonOptions = new();

    public static (int Min, int Max) ColumnRange(int column) => column switch
    {
        0 => (1, 9),
        1 => (10, 19),
        2 => (20, 29),
        3 => (30, 39),
        4 => (40, 49),
        5 => (50, 59),
        6 => (60, 69),
        7 => (70, 79),
        8 => (80, 90),
        _ => throw new ArgumentOutOfRangeException(nameof(column))
    };

    /// <summary>Generate a valid 3×9 grid (null = blank).</summary>
    public static int?[,] Generate(Random? rng = null)
    {
        rng ??= Random.Shared;
        for (var attempt = 0; attempt < 5000; attempt++)
        {
            var mask = new bool[Rows, Cols];
            var colCounts = new int[Cols];
            var ok = true;

            for (var row = 0; row < Rows; row++)
            {
                // Prefer columns with remaining capacity; shuffle for variety.
                var candidates = Enumerable.Range(0, Cols)
                    .Where(c => colCounts[c] < 3)
                    .OrderBy(_ => rng.Next())
                    .ToList();

                if (candidates.Count < NumbersPerRow)
                {
                    ok = false;
                    break;
                }

                foreach (var c in candidates.Take(NumbersPerRow))
                {
                    mask[row, c] = true;
                    colCounts[c]++;
                }
            }

            if (!ok)
            {
                continue;
            }

            var grid = new int?[Rows, Cols];
            for (var c = 0; c < Cols; c++)
            {
                var count = colCounts[c];
                if (count == 0)
                {
                    continue;
                }

                var (min, max) = ColumnRange(c);
                var pool = Enumerable.Range(min, max - min + 1)
                    .OrderBy(_ => rng.Next())
                    .Take(count)
                    .OrderBy(n => n)
                    .ToList();

                var i = 0;
                for (var r = 0; r < Rows; r++)
                {
                    if (mask[r, c])
                    {
                        grid[r, c] = pool[i++];
                    }
                }
            }

            if (IsValid(grid))
            {
                return grid;
            }
        }

        throw new InvalidOperationException("Could not generate a valid banko plate.");
    }

    public static bool IsValid(int?[,] grid)
    {
        if (grid.GetLength(0) != Rows || grid.GetLength(1) != Cols)
        {
            return false;
        }

        var all = new HashSet<int>();
        for (var r = 0; r < Rows; r++)
        {
            var rowCount = 0;
            for (var c = 0; c < Cols; c++)
            {
                var n = grid[r, c];
                if (n is null)
                {
                    continue;
                }

                rowCount++;
                var (min, max) = ColumnRange(c);
                if (n < min || n > max || !all.Add(n.Value))
                {
                    return false;
                }
            }

            if (rowCount != NumbersPerRow)
            {
                return false;
            }
        }

        if (all.Count != NumbersPerPlate)
        {
            return false;
        }

        // Numbers in a column must increase top → bottom.
        for (var c = 0; c < Cols; c++)
        {
            int? prev = null;
            for (var r = 0; r < Rows; r++)
            {
                var n = grid[r, c];
                if (n is null)
                {
                    continue;
                }

                if (prev is not null && n.Value <= prev.Value)
                {
                    return false;
                }

                prev = n;
            }
        }

        return true;
    }

    public static string Fingerprint(int?[,] grid)
    {
        var nums = new List<int>(NumbersPerPlate);
        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                if (grid[r, c] is int n)
                {
                    nums.Add(n);
                }
            }
        }

        nums.Sort();
        return string.Join(',', nums);
    }

    public static string ToJson(int?[,] grid)
    {
        var flat = new int?[Rows * Cols];
        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                flat[r * Cols + c] = grid[r, c];
            }
        }

        return JsonSerializer.Serialize(flat, JsonOptions);
    }

    public static int?[,] FromJson(string json)
    {
        var flat = JsonSerializer.Deserialize<int?[]>(json, JsonOptions)
                   ?? throw new InvalidOperationException("Invalid cells JSON.");
        if (flat.Length != Rows * Cols)
        {
            throw new InvalidOperationException("Cells JSON must have 27 entries.");
        }

        var grid = new int?[Rows, Cols];
        for (var i = 0; i < flat.Length; i++)
        {
            grid[i / Cols, i % Cols] = flat[i];
        }

        return grid;
    }

    public static IReadOnlyList<int> RowNumbers(int?[,] grid, int row)
    {
        var list = new List<int>(NumbersPerRow);
        for (var c = 0; c < Cols; c++)
        {
            if (grid[row, c] is int n)
            {
                list.Add(n);
            }
        }

        return list;
    }

    /// <summary>
    /// How many complete rows are covered by the drawn set (0–3).
    /// </summary>
    public static int CountCompletedRows(int?[,] grid, IReadOnlySet<int> drawn)
    {
        var completed = 0;
        for (var r = 0; r < Rows; r++)
        {
            var nums = RowNumbers(grid, r);
            if (nums.Count == NumbersPerRow && nums.All(drawn.Contains))
            {
                completed++;
            }
        }

        return completed;
    }

    public static IReadOnlyList<int> CompletedRowIndexes(int?[,] grid, IReadOnlySet<int> drawn)
    {
        var hit = new List<int>();
        for (var r = 0; r < Rows; r++)
        {
            var nums = RowNumbers(grid, r);
            if (nums.Count == NumbersPerRow && nums.All(drawn.Contains))
            {
                hit.Add(r + 1);
            }
        }

        return hit;
    }

    public static bool MatchesWinType(int?[,] grid, IReadOnlySet<int> drawn, Models.WinType winType)
    {
        var rows = CountCompletedRows(grid, drawn);
        return winType switch
        {
            Models.WinType.OneRow => rows >= 1,
            Models.WinType.TwoRows => rows >= 2,
            Models.WinType.FullBoard => rows >= 3,
            _ => false
        };
    }

    public static string FormatSeries(int seriesNumber) => seriesNumber.ToString("D6");

    public static bool TryParseSeries(string? input, out int seriesNumber)
    {
        seriesNumber = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out seriesNumber) && seriesNumber > 0;
    }

    /// <summary>Stable short hash for diagnostics (not for uniqueness — use Fingerprint).</summary>
    public static string ShortHash(string fingerprint)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        return Convert.ToHexString(hash.AsSpan(0, 4));
    }
}
