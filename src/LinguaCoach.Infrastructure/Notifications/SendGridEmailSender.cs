using LinguaCoach.Application.Notifications;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Sends email via SendGrid using the official SendGrid SDK.
/// PlaintextSecret in ResolvedEmailConfig holds the SendGrid API key (SG...).
/// </summary>
public sealed class SendGridEmailSender : IEmailSender
{
    private readonly INotificationChannelConfigResolver _configResolver;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        INotificationChannelConfigResolver configResolver,
        ILogger<SendGridEmailSender> logger)
    {
        _configResolver = configResolver;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var config = await _configResolver.ResolveEmailAsync(ct);

        if (!config.IsEnabled)
            return EmailSendResult.Skipped("Email sender is disabled.");

        if (string.IsNullOrWhiteSpace(config.PlaintextSecret))
            return EmailSendResult.Skipped("SendGrid API key is not configured.");

        if (string.IsNullOrWhiteSpace(config.FromAddress))
            return EmailSendResult.Skipped("Email sender has no From address configured.");

        try
        {
            var client = new SendGridClient(config.PlaintextSecret);

            var from = new EmailAddress(config.FromAddress, config.FromDisplayName ?? "SpeakPath");
            var to = new EmailAddress(message.ToAddress, message.ToDisplayName);

            var mail = MailHelper.CreateSingleEmail(
                from, to, message.Subject,
                plainTextContent: message.BodyText,
                htmlContent: message.BodyHtml);

            var response = await client.SendEmailAsync(mail, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SendGrid email sent to {To} subject={Subject} via {Source}",
                    message.ToAddress, message.Subject, config.Source);
                return EmailSendResult.Ok();
            }

            var body = await response.Body.ReadAsStringAsync(ct);
            _logger.LogError("SendGrid API error {Status} to {To}: {Body}",
                (int)response.StatusCode, message.ToAddress, body);
            return EmailSendResult.Failure($"SendGrid API returned {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendGrid send failed to {To}", message.ToAddress);
            return EmailSendResult.Failure(ex.Message);
        }
    }
}
