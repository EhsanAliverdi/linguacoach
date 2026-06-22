using System.Net;
using System.Net.Mail;
using LinguaCoach.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Sends email via SMTP using System.Net.Mail.
/// Resolves configuration at send time from INotificationChannelConfigResolver
/// (DB override → appsettings fallback). App does not crash when config is missing.
/// Tests must not use this class directly — inject IEmailSender and swap with a fake.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly INotificationChannelConfigResolver _configResolver;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        INotificationChannelConfigResolver configResolver,
        ILogger<SmtpEmailSender> logger)
    {
        _configResolver = configResolver;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var config = await _configResolver.ResolveEmailAsync(ct);

        if (!config.IsEnabled || string.IsNullOrWhiteSpace(config.Host))
            return EmailSendResult.Skipped("Email sender is disabled or host is not configured.");

        if (string.IsNullOrWhiteSpace(config.FromAddress))
            return EmailSendResult.Skipped("Email sender has no From address configured.");

        try
        {
            using var smtp = new SmtpClient(config.Host, config.Port)
            {
                EnableSsl = config.UseSsl,
                Credentials = string.IsNullOrWhiteSpace(config.Username)
                    ? null
                    : new NetworkCredential(config.Username, config.PlaintextSecret ?? string.Empty),
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            var mail = new MailMessage
            {
                From = new MailAddress(config.FromAddress, config.FromDisplayName ?? "SpeakPath"),
                Subject = message.Subject,
                IsBodyHtml = true,
                Body = message.BodyHtml,
            };

            if (!string.IsNullOrWhiteSpace(message.BodyText))
                mail.AlternateViews.Add(
                    AlternateView.CreateAlternateViewFromString(message.BodyText, null, "text/plain"));

            mail.To.Add(new MailAddress(message.ToAddress, message.ToDisplayName));

            await smtp.SendMailAsync(mail, ct);

            _logger.LogInformation("Email sent to {To} subject={Subject} via {Source}",
                message.ToAddress, message.Subject, config.Source);
            return EmailSendResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed to {To}", message.ToAddress);
            return EmailSendResult.Failure(ex.Message);
        }
    }
}
