using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Body,
    NotificationCategory Category,
    NotificationSeverity Severity,
    NotificationChannel Channel,
    NotificationStatus Status,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    DateTime? ExpiresAtUtc,
    string? DeepLinkUrl,
    string? MetadataJson
);

public sealed record PagedNotificationResult(
    IReadOnlyList<NotificationDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
