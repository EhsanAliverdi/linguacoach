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

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IAdminNotificationHandler
{
    Task<PagedResponse<AdminNotificationItem>> ListNotificationsAsync(
        AdminNotificationListQuery query, CancellationToken ct = default);

    Task<PagedResponse<AdminOutboxItem>> ListOutboxAsync(
        AdminOutboxListQuery query, CancellationToken ct = default);

    Task RetryOutboxItemAsync(Guid outboxItemId, Guid adminUserId, CancellationToken ct = default);

    Task CancelOutboxItemAsync(Guid outboxItemId, Guid adminUserId, CancellationToken ct = default);
}
