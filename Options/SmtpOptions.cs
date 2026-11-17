namespace Julebanko.Options;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "outbound12.electric.net";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "julebanko@example.com";
}
