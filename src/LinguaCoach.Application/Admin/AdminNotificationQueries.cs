namespace LinguaCoach.Application.Admin;

// ── Query inputs ──────────────────────────────────────────────────────────────

public sealed record AdminNotificationListQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? RecipientUserId = null,
    string? Channel = null,
    string? Status = null,
    string? Category = null,
    string? Severity = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null);

public sealed record AdminOutboxListQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? RecipientUserId = null,
    string? Channel = null,
    string? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    bool DueOnly = false,
    bool FailedOnly = false);

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record AdminNotificationItem(
    Guid Id,
    Guid RecipientUserId,
    string RecipientEmail,
    string Title,
    string Body,
    string Channel,
    string Category,
    string Severity,
    string Status,
    string? DeepLinkUrl,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    DateTime? ExpiresAtUtc);

public sealed record AdminOutboxItem(
    Guid Id,
    Guid? NotificationId,
    Guid RecipientUserId,
    string RecipientEmail,
    string Channel,
    string Status,
    int AttemptCount,
    DateTime CreatedAtUtc,
    DateTime? NextAttemptAtUtc,
    DateTime? LastAttemptAtUtc,
    DateTime? ProcessedAtUtc,
    string? LastError);

// ── Send command + result ─────────────────────────────────────────────────────

public sealed record AdminSendNotificationCommand(
    IReadOnlyList<Guid> RecipientUserIds,
    IReadOnlyList<string> Channels,
    string Title,
    string Body,
    string Category,
    string Severity,
    string? DeepLinkUrl,
    DateTime? ExpiresAtUtc);

public sealed record AdminSendNotificationResult(
    int RequestedRecipientCount,
    int QueuedCount,
    int SkippedCount,
    IReadOnlyList<string> ChannelsQueued,
    IReadOnlyList<string> Errors);

// ── Config status ─────────────────────────────────────────────────────────────

public sealed record AdminNotificationConfigStatus(
    AdminChannelStatus InApp,
    AdminEmailConfigStatus Email,
    AdminSmsConfigStatus Sms,
    AdminDispatchJobStatus DispatchJob);

public sealed record AdminChannelStatus(
    string Channel,
    bool Enabled,
    string StatusLabel);

public sealed record AdminEmailConfigStatus(
    bool Enabled,
    bool Configured,
    string StatusLabel,
    string? Host,
    int Port,
    string? FromAddress,
    string? FromDisplayName,
    bool UseSsl,
    bool HasUsername,
    bool HasPassword);

public sealed record AdminSmsConfigStatus(
    bool Enabled,
    bool Configured,
    string StatusLabel,
    string? Provider,
    string? SenderId,
    bool HasApiKey);

public sealed record AdminDispatchJobStatus(
    bool Enabled,
    string IntervalDescription,
    int BatchSize);

public sealed record AdminTestEmailResult(
    bool Succeeded,
    bool WasSkipped,
    string? Message);

// ── Config source ─────────────────────────────────────────────────────────────

public enum NotificationConfigSource
{
    AppSettings,
    Database,
    Mixed,
}

// ── Config status (extended with source) ─────────────────────────────────────

public sealed record AdminNotificationConfigStatusV2(
    AdminChannelStatus InApp,
    AdminEmailConfigStatus Email,
    AdminSmsConfigStatus Sms,
    AdminDispatchJobStatus DispatchJob,
    string Source);   // "AppSettings" | "Database" | "Mixed"

// ── Update commands ───────────────────────────────────────────────────────────

public sealed record AdminUpdateEmailConfigCommand(
    bool IsEnabled,
    /// <summary>Provider: "Smtp", "Resend", or "SendGrid". Null defaults to Smtp.</summary>
    string? Provider,
    string? Host,
    int? Port,
    bool? UseSsl,
    string? FromAddress,
    string? FromDisplayName,
    string? Username,
    /// <summary>New plaintext secret / API key. Null = leave unchanged.</summary>
    string? NewSecret,
    /// <summary>Explicitly clear the stored secret.</summary>
    bool ClearSecret);

public sealed record AdminUpdateSmsConfigCommand(
    bool IsEnabled,
    string? Provider,
    string? SenderId,
    string? NewSecret,
    bool ClearSecret);

public sealed record AdminUpdateInAppConfigCommand(
    bool IsEnabled);

public sealed record AdminUpdateConfigResult(
    bool Succeeded,
    string Message,
    string Source);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IAdminNotificationHandler
{
    Task<PagedResponse<AdminNotificationItem>> ListNotificationsAsync(
        AdminNotificationListQuery query, CancellationToken ct = default);

    Task<PagedResponse<AdminOutboxItem>> ListOutboxAsync(
        AdminOutboxListQuery query, CancellationToken ct = default);

    Task RetryOutboxItemAsync(Guid outboxItemId, Guid adminUserId, CancellationToken ct = default);

    Task CancelOutboxItemAsync(Guid outboxItemId, Guid adminUserId, CancellationToken ct = default);

    Task<AdminSendNotificationResult> SendNotificationAsync(
        AdminSendNotificationCommand command, Guid adminUserId, CancellationToken ct = default);

    Task<AdminNotificationConfigStatus> GetConfigStatusAsync(CancellationToken ct = default);

    Task<AdminNotificationConfigStatusV2> GetConfigStatusV2Async(CancellationToken ct = default);

    Task<AdminUpdateConfigResult> UpdateEmailConfigAsync(
        AdminUpdateEmailConfigCommand command, Guid adminUserId, CancellationToken ct = default);

    Task<AdminUpdateConfigResult> UpdateSmsConfigAsync(
        AdminUpdateSmsConfigCommand command, Guid adminUserId, CancellationToken ct = default);

    Task<AdminUpdateConfigResult> UpdateInAppConfigAsync(
        AdminUpdateInAppConfigCommand command, Guid adminUserId, CancellationToken ct = default);

    Task<AdminTestEmailResult> TestEmailAsync(string toAddress, Guid adminUserId, CancellationToken ct = default);
}
