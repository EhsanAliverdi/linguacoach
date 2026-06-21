using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Notifications;

public interface INotificationService
{
    Task QueueAsync(NotificationRequest request, CancellationToken ct = default);

    Task QueueInAppAsync(
        Guid recipientUserId,
        string title,
        string body,
        NotificationCategory category,
        NotificationSeverity severity,
        string? deepLinkUrl = null,
        DateTime? expiresAtUtc = null,
        CancellationToken ct = default);

    Task QueueEmailAsync(
        Guid recipientUserId,
        string title,
        string body,
        NotificationCategory category,
        NotificationSeverity severity,
        string? deepLinkUrl = null,
        DateTime? expiresAtUtc = null,
        CancellationToken ct = default);

    Task QueueSmsAsync(
        Guid recipientUserId,
        string title,
        string body,
        NotificationCategory category,
        NotificationSeverity severity,
        DateTime? expiresAtUtc = null,
        CancellationToken ct = default);
}
