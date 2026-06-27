namespace LinguaCoach.Application.Notifications;

/// <summary>
/// Resolved email config for runtime use. Contains plaintext secret — never return in API responses.
/// </summary>
public sealed record ResolvedEmailConfig(
    bool IsEnabled,
    /// <summary>Provider: "Smtp", "Resend", "SendGrid". Null defaults to Smtp.</summary>
    string? Provider,
    string? Host,
    int Port,
    bool UseSsl,
    string? FromAddress,
    string? FromDisplayName,
    string? Username,
    /// <summary>Decrypted secret / API key. Null means no credential stored.</summary>
    string? PlaintextSecret,
    /// <summary>Source: "AppSettings" or "Database".</summary>
    string Source);

/// <summary>
/// Resolved SMS config for runtime use. Contains plaintext secret — never return in API responses.
/// </summary>
public sealed record ResolvedSmsConfig(
    bool IsEnabled,
    string? Provider,
    string? SenderId,
    string? PlaintextSecret,
    string Source);

/// <summary>
/// Resolves effective notification channel configuration.
/// DB row wins over appsettings when present.
/// Safe fallback — never throws when DB is unavailable; returns disabled config instead.
/// </summary>
public interface INotificationChannelConfigResolver
{
    Task<ResolvedEmailConfig> ResolveEmailAsync(CancellationToken ct = default);
    Task<ResolvedSmsConfig> ResolveSmsAsync(CancellationToken ct = default);
}
