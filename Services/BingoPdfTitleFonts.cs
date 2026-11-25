using QuestPDF.Drawing;

namespace Julebanko.Services;

/// <summary>
/// Printable PDF document-title font + size presets (Setup). Grid cell numbers stay on the default face.
/// Embedded TTF files under <c>Fonts/</c> are registered once for Linux/Windows IIS parity.
/// </summary>
public static class BingoPdfTitleFonts
{
    public const string DefaultFontId = "liberation-serif";
    public const int DefaultSize = 24;

    public sealed record FontOption(string Id, string Label, string FileName);

    public static readonly FontOption[] Fonts =
    [
        new("liberation-serif", "Liberation Serif", "LiberationSerif-Bold.ttf"),
        new("tinos", "Tinos (Times-like)", "Tinos-Bold.ttf"),
        new("dejavu-serif", "DejaVu Serif", "DejaVuSerif-Bold.ttf"),
        new("liberation-sans", "Liberation Sans", "LiberationSans-Bold.ttf"),
        new("arimo", "Arimo (Arial-like)", "Arimo-Bold.ttf"),
        new("dejavu-sans", "DejaVu Sans", "DejaVuSans-Bold.ttf"),
    ];

    /// <summary>Limited presets so the page header stays printable on A4 with 6 plates.</summary>
    public static readonly int[] Sizes = [18, 20, 22, 24, 26, 28, 32];

    private static int _registered;

    public static FontOption Resolve(string? id) =>
        Fonts.FirstOrDefault(f => f.Id == id) ?? Fonts[0];

    public static int ResolveSize(string? raw)
    {
        if (int.TryParse(raw, out var n) && Sizes.Contains(n))
        {
            return n;
        }

        return DefaultSize;
    }

    public static int ResolveSize(int size) => Sizes.Contains(size) ? size : DefaultSize;

    /// <summary>Register embedded title fonts with QuestPDF (idempotent).</summary>
    public static void EnsureRegistered(string contentRoot)
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        var fontsDir = Path.Combine(contentRoot, "Fonts");
        foreach (var font in Fonts)
        {
            var path = Path.Combine(fontsDir, font.FileName);
            if (!File.Exists(path))
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            // Custom name = option Id so PDF TextStyle can use FontFamily(id) reliably.
            FontManager.RegisterFontWithCustomName(font.Id, stream);
        }
    }
}
