namespace LinguaCoach.Application.Notifications;

/// <summary>
/// Outbound email message passed to IEmailSender.
/// Do not put raw passwords or secrets in any field.
/// </summary>
public sealed record EmailMessage(
    string ToAddress,
    string ToDisplayName,
    string Subject,
    string BodyHtml,
    string? BodyText = null);

/// <summary>
/// Result returned by IEmailSender.SendAsync — never throws for transient/config issues.
/// </summary>
public sealed record EmailSendResult(bool Succeeded, bool WasSkipped, string? Error)
{
    public static EmailSendResult Ok()      => new(true,  false, null);
    public static EmailSendResult Skipped(string reason) => new(false, true,  reason);
    public static EmailSendResult Failure(string error)  => new(false, false, error);
}

/// <summary>
/// Abstraction for sending email. Implementations:
///   DisabledEmailSender  — always skips (missing/disabled config)
///   SmtpEmailSender      — sends via SMTP
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}
