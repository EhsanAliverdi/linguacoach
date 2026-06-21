namespace LinguaCoach.Application.Notifications;

public interface INotificationQueryService
{
    Task<PagedNotificationResult> ListAsync(NotificationListQuery query, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
    Task ArchiveAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
}
