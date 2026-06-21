using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Notifications;

public sealed record NotificationListQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    bool UnreadOnly = false,
    NotificationCategory? Category = null,
    NotificationSeverity? Severity = null
);
