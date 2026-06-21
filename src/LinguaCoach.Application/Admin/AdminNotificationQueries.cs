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
    AdminChannelStatus Sms,
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

public sealed record AdminDispatchJobStatus(
    bool Enabled,
    string IntervalDescription,
    int BatchSize);

public sealed record AdminTestEmailResult(
    bool Succeeded,
    bool WasSkipped,
    string? Message);

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

    Task<AdminTestEmailResult> TestEmailAsync(string toAddress, Guid adminUserId, CancellationToken ct = default);
}
