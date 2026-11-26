using Julebanko.Data;

namespace Julebanko.Services;

public static class CultureSupport
{
    public const string Danish = "da";
    public const string English = "en";
    public const string Swedish = "sv";

    public const string DefaultBrandDa = "Julebanko";
    public const string DefaultBrandEn = "Christmas Bingo";
    public const string DefaultBrandSv = "Julbingo";

    public static readonly string[] Supported = [Danish, English, Swedish];

    public static string Normalize(string? culture) => culture switch
    {
        English => English,
        Swedish => Swedish,
        _ => Danish
    };

    public static bool IsSupported(string? culture) =>
        culture is Danish or English or Swedish;

    public static string BrandSettingKey(string? culture) => Normalize(culture) switch
    {
        English => SettingKeys.BrandNameEn,
        Swedish => SettingKeys.BrandNameSv,
        _ => SettingKeys.BrandNameDa
    };

    public static string DefaultBrandName(string? culture) => Normalize(culture) switch
    {
        English => DefaultBrandEn,
        Swedish => DefaultBrandSv,
        _ => DefaultBrandDa
    };
}
