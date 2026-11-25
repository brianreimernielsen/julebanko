using System.Globalization;
using System.Text.RegularExpressions;

namespace Julebanko.Services;

/// <summary>
/// Normalize and validate CSS hex colors. UI may omit '#'; stored values use #RRGGBB.
/// </summary>
public static partial class ColorHex
{
    [GeneratedRegex(@"^#?([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexPattern();

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        var match = HexPattern().Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        var digits = match.Groups[1].Value;
        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(c => $"{c}{c}"));
        }

        normalized = "#" + digits.ToUpperInvariant();
        return true;
    }

    /// <summary>Hex without leading # for display in the shared picker.</summary>
    public static string DisplayDigits(string? storedOrRaw)
    {
        if (TryNormalize(storedOrRaw, out var n))
        {
            return n[1..];
        }

        var t = (storedOrRaw ?? "").Trim().TrimStart('#');
        return t.Length <= 6 ? t.ToUpperInvariant() : t[..6].ToUpperInvariant();
    }

    public static (int R, int G, int B)? ToRgb(string? hex)
    {
        if (!TryNormalize(hex, out var n))
        {
            return null;
        }

        var r = int.Parse(n.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(n.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(n.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (r, g, b);
    }

    public static string OrDefault(string? input, string fallback)
    {
        return TryNormalize(input, out var n) ? n : fallback;
    }
}
