namespace Julebanko.Data;

public static class SettingKeys
{
    public const string PlayerBgColor = "PlayerBgColor";
    public const string AdminBgColor = "AdminBgColor";
    public const string AccentColor = "AccentColor";
    public const string PlayerTextColor = "PlayerTextColor";
    public const string AdminTextColor = "AdminTextColor";
    public const string ClaimNotifyTo = "ClaimNotifyTo";
    public const string MailFrom = "MailFrom";
    public const string SmtpHost = "SmtpHost";
    public const string SmtpPort = "SmtpPort";
    public const string SmtpUseSsl = "SmtpUseSsl";
    public const string SmtpUsername = "SmtpUsername";
    public const string SmtpPassword = "SmtpPassword";
    public const string SeasonTitle = "SeasonTitle";
    public const string PlayerSnowEnabled = "PlayerSnowEnabled";
    public const string PlayerSnowIntensity = "PlayerSnowIntensity";
    public const string PlayerSnowGroundEnabled = "PlayerSnowGroundEnabled";
    public const string PlayerNisseEnabled = "PlayerNisseEnabled";
    public const string DefaultCulture = "DefaultCulture";
    public const string BrandNameDa = "BrandNameDa";
    public const string BrandNameEn = "BrandNameEn";
    public const string BrandNameSv = "BrandNameSv";
    public const string BrandFont = "BrandFont";
    public const string BodyFont = "BodyFont";

    /// <summary>When true, claims are checked against plate + drawn numbers on submit.</summary>
    public const string BingoAutoValidate = "BingoAutoValidate";

    /// <summary>When true (default), each player may win each win type at most once per season.</summary>
    public const string BingoPrizeOncePerType = "BingoPrizeOncePerType";

    /// <summary>Optional banner override printed on PDF plates (falls back to BrandName).</summary>
    public const string BingoBannerText = "BingoBannerText";

    public const string BingoPrimaryColor = "BingoPrimaryColor";
    public const string BingoAccentColor = "BingoAccentColor";
    public const string BingoFestiveLook = "BingoFestiveLook";
}
