using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Per-user per-category per-channel opt-in/out preference.
/// Account/Security categories are required and cannot be disabled.
/// </summary>
public sealed class NotificationPreference
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationCategory Category { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private NotificationPreference() { }

    public static NotificationPreference Create(
        Guid userId,
        NotificationCategory category,
        NotificationChannel channel,
        bool isEnabled)
    {
        return new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Category = category,
            Channel = channel,
            IsEnabled = isEnabled,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Categories that cannot be disabled by the user — always deliver.
    /// </summary>
    public static bool IsRequired(NotificationCategory category) =>
        category is NotificationCategory.Account or NotificationCategory.System;
}
