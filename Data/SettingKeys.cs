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

    /// <summary>Default PDF plate banner (Setup). Empty → BrandName. Batch banner overrides this.</summary>
    public const string BingoBannerText = "BingoBannerText";

    /// <summary>Legacy; prefer BingoPlateScheme + BingoFrameColor / BingoBannerColor.</summary>
    public const string BingoPrimaryColor = "BingoPrimaryColor";

    /// <summary>Legacy; series color now comes from BingoPlateScheme.</summary>
    public const string BingoAccentColor = "BingoAccentColor";

    /// <summary>Deprecated — festive checkbox removed; keep key for old DBs.</summary>
    public const string BingoFestiveLook = "BingoFestiveLook";

    /// <summary>Id from <see cref="Services.BingoPlateSchemes"/> (two contrasting plate colors; series follows).</summary>
    public const string BingoPlateScheme = "BingoPlateScheme";

    /// <summary>PDF page paper color under decorative page backgrounds (default parchment).</summary>
    public const string BingoPdfBackgroundColor = "BingoPdfBackgroundColor";

    /// <summary>Plate frame / border color.</summary>
    public const string BingoFrameColor = "BingoFrameColor";

    /// <summary>Banner text color on plates and page header.</summary>
    public const string BingoBannerColor = "BingoBannerColor";

    /// <summary>PDF document-title font id from <see cref="Services.BingoPdfTitleFonts"/>.</summary>
    public const string BingoPdfTitleFont = "BingoPdfTitleFont";

    /// <summary>PDF document-title font size (pt) from <see cref="Services.BingoPdfTitleFonts.Sizes"/>.</summary>
    public const string BingoPdfTitleSize = "BingoPdfTitleSize";

    /// <summary>When true (default), plate header shows Code128 + series digits; when false, series digits only.</summary>
    public const string BingoShowBarcode = "BingoShowBarcode";

    /// <summary>When true, footer shows «Brand page/total». Default false (Brian: optional).</summary>
    public const string BingoShowPageNumbers = "BingoShowPageNumbers";
}
