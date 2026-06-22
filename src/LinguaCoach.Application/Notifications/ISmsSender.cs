namespace LinguaCoach.Application.Notifications;

public sealed record SmsMessage(
    string ToPhoneNumber,
    string Body);

public sealed record SmsSendResult(bool Succeeded, bool WasSkipped, string? Error)
{
    public static SmsSendResult Ok()                          => new(true,  false, null);
    public static SmsSendResult Skipped(string reason)       => new(false, true,  reason);
    public static SmsSendResult Failure(string error)        => new(false, false, error);
}

/// <summary>
/// Abstraction for sending SMS. Implementations:
///   DisabledSmsSender — always skips (SMS deferred or config missing).
///   Future: TwilioSmsSender, etc.
/// </summary>
public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct = default);
}
