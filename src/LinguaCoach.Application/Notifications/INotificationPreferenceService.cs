using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Notifications;

public sealed record NotificationPreferenceItem(
    NotificationCategory Category,
    NotificationChannel Channel,
    bool IsEnabled,
    bool IsRequired);

public sealed record UpdateNotificationPreferenceRequest(
    NotificationCategory Category,
    NotificationChannel Channel,
    bool IsEnabled);

public interface INotificationPreferenceService
{
    /// <summary>
    /// Returns true if the channel is enabled for this user/category.
    /// Always returns true for required (Account/System) categories.
    /// Always returns false for SMS (deferred).
    /// Returns the default when no explicit preference row exists.
    /// </summary>
    Task<bool> IsChannelEnabledAsync(
        Guid userId,
        NotificationCategory category,
        NotificationChannel channel,
        CancellationToken ct = default);

    Task<IReadOnlyList<NotificationPreferenceItem>> GetPreferencesAsync(
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts preferences. Required categories are silently enforced (IsEnabled forced true).
    /// SMS preferences are accepted but have no delivery effect.
    /// </summary>
    Task UpdatePreferencesAsync(
        Guid userId,
        IEnumerable<UpdateNotificationPreferenceRequest> preferences,
        CancellationToken ct = default);
}
