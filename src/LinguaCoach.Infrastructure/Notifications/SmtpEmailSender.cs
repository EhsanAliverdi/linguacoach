using System.Net;
using System.Net.Mail;
using LinguaCoach.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Sends email via SMTP using System.Net.Mail.
/// Activated when Email:Enabled = true and Email:Host is set.
/// Tests must not use this class directly — inject IEmailSender and swap with a fake.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Host))
            return EmailSendResult.Skipped("Email sender is disabled or host is not configured.");

        try
        {
            using var smtp = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
                Credentials = string.IsNullOrWhiteSpace(_options.Username)
                    ? null
                    : new NetworkCredential(_options.Username, _options.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            var mail = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromDisplayName),
                Subject = message.Subject,
                IsBodyHtml = true,
                Body = message.BodyHtml,
            };

            if (!string.IsNullOrWhiteSpace(message.BodyText))
                mail.AlternateViews.Add(
                    AlternateView.CreateAlternateViewFromString(message.BodyText, null, "text/plain"));

            mail.To.Add(new MailAddress(message.ToAddress, message.ToDisplayName));

            await smtp.SendMailAsync(mail, ct);

            _logger.LogInformation("Email sent to {To} subject={Subject}", message.ToAddress, message.Subject);
            return EmailSendResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed to {To}", message.ToAddress);
            return EmailSendResult.Failure(ex.Message);
        }
    }
}
