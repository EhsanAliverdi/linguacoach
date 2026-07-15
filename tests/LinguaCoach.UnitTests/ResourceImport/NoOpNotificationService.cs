using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>Shared no-op INotificationService test double — the Import Execution Plan services
/// queue admin notifications at several lifecycle points; tests that don't assert on
/// notification content just need a harmless stub.</summary>
internal sealed class NoOpNotificationService : INotificationService
{
    public Task QueueAsync(NotificationRequest request, CancellationToken ct = default) => Task.CompletedTask;

    public Task QueueInAppAsync(
        Guid recipientUserId, string title, string body, NotificationCategory category, NotificationSeverity severity,
        string? deepLinkUrl = null, DateTime? expiresAtUtc = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task QueueEmailAsync(
        Guid recipientUserId, string title, string body, NotificationCategory category, NotificationSeverity severity,
        string? deepLinkUrl = null, DateTime? expiresAtUtc = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task QueueSmsAsync(
        Guid recipientUserId, string title, string body, NotificationCategory category, NotificationSeverity severity,
        DateTime? expiresAtUtc = null, CancellationToken ct = default) => Task.CompletedTask;
}
