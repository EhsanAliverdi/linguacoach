using LinguaCoach.Application.Notifications;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// No-op SMS sender used when SMS is disabled or not yet configured.
/// Never throws — always returns a safe skipped result.
/// </summary>
public sealed class DisabledSmsSender : ISmsSender
{
    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct = default)
        => Task.FromResult(SmsSendResult.Skipped("SMS is not enabled. Configure Sms:Enabled=true and provider credentials to activate."));
}
