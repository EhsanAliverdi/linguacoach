using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Notifications;

public sealed record NotificationRequest(
    Guid RecipientUserId,
    string Title,
    string Body,
    NotificationChannel Channel,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string? DeepLinkUrl = null,
    DateTime? ExpiresAtUtc = null,
    string? MetadataJson = null
);
