namespace Julebanko.Services;

/// <summary>
/// Curated display/body fonts for Setup Admin. Values are stable AppSettings keys.
/// </summary>
public static class FontCatalog
{
    public const string DefaultBrandFont = "fraunces";
    public const string DefaultBodyFont = "outfit";

    public sealed record FontOption(
        string Id,
        string Label,
        string CssFamily,
        string GoogleFamilyQuery,
        bool IsDisplay);

    public static readonly FontOption[] BrandFonts =
    [
        new("fraunces", "Fraunces (serif)", "\"Fraunces\", \"Palatino Linotype\", Georgia, serif",
            "Fraunces:opsz,wght@9..144,600;9..144,700", true),
        new("playfair", "Playfair Display (serif)", "\"Playfair Display\", Georgia, serif",
            "Playfair+Display:wght@600;700", true),
        new("cinzel", "Cinzel (display serif)", "\"Cinzel\", \"Times New Roman\", serif",
            "Cinzel:wght@600;700", true),
        new("cormorant", "Cormorant Garamond (soft serif)", "\"Cormorant Garamond\", Georgia, serif",
            "Cormorant+Garamond:wght@600;700", true),
        new("libre-baskerville", "Libre Baskerville (classic)", "\"Libre Baskerville\", Georgia, serif",
            "Libre+Baskerville:wght@700", true),
        new("nunito", "Nunito (soft sans)", "\"Nunito\", \"Segoe UI\", sans-serif",
            "Nunito:wght@600;700;800", true),
    ];

    public static readonly FontOption[] BodyFonts =
    [
        new("outfit", "Outfit", "\"Outfit\", \"Segoe UI\", sans-serif",
            "Outfit:wght@400;500;600;700", false),
        new("source-sans", "Source Sans 3", "\"Source Sans 3\", \"Segoe UI\", sans-serif",
            "Source+Sans+3:wght@400;500;600;700", false),
        new("nunito", "Nunito", "\"Nunito\", \"Segoe UI\", sans-serif",
            "Nunito:wght@400;500;600;700", false),
        new("dm-sans", "DM Sans", "\"DM Sans\", \"Segoe UI\", sans-serif",
            "DM+Sans:wght@400;500;600;700", false),
    ];

    public static FontOption ResolveBrand(string? id) =>
        BrandFonts.FirstOrDefault(f => f.Id == id) ?? BrandFonts[0];

    public static FontOption ResolveBody(string? id) =>
        BodyFonts.FirstOrDefault(f => f.Id == id) ?? BodyFonts[0];

    public static string GoogleFontsHref(string? brandId, string? bodyId)
    {
        var brand = ResolveBrand(brandId);
        var body = ResolveBody(bodyId);
        var families = new HashSet<string>(StringComparer.Ordinal)
        {
            $"family={brand.GoogleFamilyQuery}",
            $"family={body.GoogleFamilyQuery}"
        };
        return "https://fonts.googleapis.com/css2?" + string.Join("&", families) + "&display=swap";
    }
}
