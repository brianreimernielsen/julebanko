namespace Julebanko.Services;

/// <summary>
/// Predefined high-contrast plate color pairs. ColorA = plate chrome / fill tint;
/// ColorB = series number (follows the scheme). Frame and banner text are separate settings.
/// </summary>
public sealed record BingoPlateScheme(
    string Id,
    string NameDa,
    string NameEn,
    string NameSv,
    string ColorA,
    string ColorB);

public static class BingoPlateSchemes
{
    public const string DefaultId = "crimson-gold";

    public static readonly IReadOnlyList<BingoPlateScheme> All =
    [
        new("crimson-gold", "Karmesin / guld", "Crimson / gold", "Karmosin / guld", "#8B1E1E", "#D4A017"),
        new("forest-cream", "Skovgrøn / creme", "Forest / cream", "Skogsgrön / grädde", "#1B4332", "#F4E8C1"),
        new("navy-amber", "Marine / rav", "Navy / amber", "Marin / bärnsten", "#1A365D", "#E6A23C"),
        new("plum-mint", "Blomme / mint", "Plum / mint", "Plommon / mint", "#5B2C6F", "#A8E6CF"),
        new("slate-coral", "Skifer / koral", "Slate / coral", "Skiffer / korall", "#2F3E46", "#FF6B6B"),
        new("burgundy-ivory", "Bourgogne / ivory", "Burgundy / ivory", "Bourgogne / ivory", "#6B0F1A", "#FFF8E7"),
        new("teal-sand", "Teal / sand", "Teal / sand", "Teal / sand", "#0D5C63", "#E8D5B7"),
        new("ink-gold", "Blæk / guld", "Ink / gold", "Bläck / guld", "#111827", "#C9A227"),
        new("pine-berry", "Fyr / bær", "Pine / berry", "Tall / bär", "#2D6A4F", "#E63946"),
        new("cobalt-snow", "Kobolt / sne", "Cobalt / snow", "Kobolt / snö", "#1E3A8A", "#F8FAFC"),
        new("cocoa-copper", "Kakao / kobber", "Cocoa / copper", "Kakao / koppar", "#4A2C2A", "#C97B4A"),
        new("charcoal-lime", "Kul / lime", "Charcoal / lime", "Kol / lime", "#36454F", "#C5E063"),
        new("wine-champagne", "Vin / champagne", "Wine / champagne", "Vin / champagne", "#722F37", "#F7E7CE"),
        new("midnight-rose", "Midnat / rose", "Midnight / rose", "Midnatt / ros", "#0B132B", "#E8A0BF"),
        new("olive-cream", "Oliven / creme", "Olive / cream", "Oliv / grädde", "#556B2F", "#FAF3E0")
    ];

    public static BingoPlateScheme GetOrDefault(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var found = All.FirstOrDefault(s =>
                string.Equals(s.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                return found;
            }
        }

        return All.First(s => s.Id == DefaultId);
    }

    public static string LocalizedName(BingoPlateScheme scheme, string cultureTwoLetter) =>
        cultureTwoLetter.ToLowerInvariant() switch
        {
            "da" => scheme.NameDa,
            "sv" => scheme.NameSv,
            _ => scheme.NameEn
        };
}
