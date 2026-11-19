namespace Julebanko.Services;

public static class CultureSupport
{
    public const string Danish = "da";
    public const string English = "en";
    public const string Swedish = "sv";

    public static readonly string[] Supported = [Danish, English, Swedish];

    public static string Normalize(string? culture) => culture switch
    {
        English => English,
        Swedish => Swedish,
        _ => Danish
    };

    public static bool IsSupported(string? culture) =>
        culture is Danish or English or Swedish;
}
