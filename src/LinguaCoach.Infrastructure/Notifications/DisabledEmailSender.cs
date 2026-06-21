using LinguaCoach.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Used when Email:Enabled is false or configuration is missing.
/// Never throws; returns a Skipped result with a diagnostic reason.
/// </summary>
public sealed class DisabledEmailSender : IEmailSender
{
    private readonly ILogger<DisabledEmailSender> _logger;

    public DisabledEmailSender(ILogger<DisabledEmailSender> logger)
    {
        _logger = logger;
    }

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Email to {To} skipped — email sender is disabled or not configured.",
            message.ToAddress);

        return Task.FromResult(
            EmailSendResult.Skipped("Email sender is disabled or not configured."));
    }
}
