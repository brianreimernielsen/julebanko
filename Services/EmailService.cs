using Julebanko.Data;
using Julebanko.Models;
using Julebanko.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Julebanko.Services;

public class EmailService(
    IOptions<SmtpOptions> smtpOptions,
    SettingsService settings,
    ILogger<EmailService> logger)
{
    public async Task SendClaimNotificationAsync(Claim claim)
    {
        var smtp = await ResolveSmtpAsync();
        var to = await settings.GetAsync(SettingKeys.ClaimNotifyTo, "banko@example.com");
        var from = string.IsNullOrWhiteSpace(smtp.From) ? "julebanko@example.com" : smtp.From;
        var brand = await settings.GetBrandNameAsync("da");
        var winLabel = ClaimService.WinTypeLabel(claim.WinType, "da");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = $"{brand} claim: {claim.PlayerName} — {winLabel}";
        message.Body = new TextPart("plain")
        {
            Text =
                $"""
                Ny banko-melding / New claim

                Navn / Name: {claim.PlayerName}
                Type: {winLabel}
                Tidspunkt / Time (UTC): {claim.CreatedAt:yyyy-MM-dd HH:mm:ss}
                Status: Pending

                Godkend eller afvis i Banko Admin.
                """
        };

        try
        {
            using var client = new SmtpClient();
            var secure = smtp.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(smtp.Host, smtp.Port, secure);

            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            logger.LogInformation("Claim email sent to {To} for claim {ClaimId}", to, claim.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send claim email to {To} for claim {ClaimId}", to, claim.Id);
            throw;
        }
    }

    /// <summary>
    /// AppSettings overrides when present; otherwise appsettings.json Smtp section.
    /// </summary>
    public async Task<SmtpOptions> ResolveSmtpAsync()
    {
        var file = smtpOptions.Value;
        var host = await settings.GetAsync(SettingKeys.SmtpHost, file.Host);
        var portRaw = await settings.GetAsync(SettingKeys.SmtpPort, file.Port.ToString());
        var sslRaw = await settings.GetAsync(SettingKeys.SmtpUseSsl, file.UseSsl ? "true" : "false");
        var username = await settings.GetAsync(SettingKeys.SmtpUsername, file.Username ?? "");
        var password = await settings.GetAsync(SettingKeys.SmtpPassword, file.Password ?? "");
        var from = await settings.GetAsync(SettingKeys.MailFrom, file.From);

        if (!int.TryParse(portRaw, out var port) || port <= 0)
        {
            port = file.Port > 0 ? file.Port : 587;
        }

        var useSsl = !string.Equals(sslRaw, "false", StringComparison.OrdinalIgnoreCase) && sslRaw != "0";

        return new SmtpOptions
        {
            Host = string.IsNullOrWhiteSpace(host) ? file.Host : host.Trim(),
            Port = port,
            UseSsl = useSsl,
            Username = username,
            Password = password,
            From = string.IsNullOrWhiteSpace(from) ? file.From : from.Trim()
        };
    }
}
