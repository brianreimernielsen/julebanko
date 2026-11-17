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
        var smtp = smtpOptions.Value;
        var to = await settings.GetAsync(SettingKeys.ClaimNotifyTo, "banko@example.com");
        var from = await settings.GetAsync(SettingKeys.MailFrom, smtp.From);
        var winLabel = ClaimService.WinTypeLabel(claim.WinType, "da");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = $"Julebanko claim: {claim.PlayerName} — {winLabel}";
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
}
