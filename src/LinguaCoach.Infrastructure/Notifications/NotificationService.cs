using System.Text.Json;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly LinguaCoachDbContext _db;
    private readonly INotificationPreferenceService _preferences;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        LinguaCoachDbContext db,
        INotificationPreferenceService preferences,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _preferences = preferences;
        _logger = logger;
    }

    public async Task QueueAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var enabled = await _preferences.IsChannelEnabledAsync(
            request.RecipientUserId, request.Category, request.Channel, ct);

        if (!enabled)
        {
            _logger.LogDebug(
                "Notification skipped due to user preference: {Channel}/{Category} for user {UserId}.",
                request.Channel, request.Category, request.RecipientUserId);
            return;
        }

        var notification = Notification.Create(
            request.RecipientUserId,
            request.Title,
            request.Body,
            request.Channel,
            request.Category,
            request.Severity,
            request.DeepLinkUrl,
            request.ExpiresAtUtc,
            request.MetadataJson);

        _db.Notifications.Add(notification);

        var payload = BuildPayload(notification);
        var outbox = NotificationOutboxItem.Create(
            request.RecipientUserId,
            request.Channel,
            payload,
            notification.Id);

        _db.NotificationOutboxItems.Add(outbox);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Notification queued: {Channel} for user {UserId} — {Title}",
            request.Channel, request.RecipientUserId, request.Title);
    }

    public Task QueueInAppAsync(
        Guid recipientUserId,
        string title,
        string body,
        NotificationCategory category,
        NotificationSeverity severity,
        string? deepLinkUrl = null,
        DateTime? expiresAtUtc = null,
        CancellationToken ct = default) =>
        QueueAsync(new NotificationRequest(
            recipientUserId, title, body,
            NotificationChannel.InApp, category, severity,
            deepLinkUrl, expiresAtUtc), ct);

    public Task QueueEmailAsync(
        Guid recipientUserId,
        string title,
        string body,
        NotificationCategory category,
        NotificationSeverity severity,
        string? deepLinkUrl = null,
        DateTime? expiresAtUtc = null,
        CancellationToken ct = default) =>
        QueueAsync(new NotificationRequest(
            recipientUserId, title, body,
            NotificationChannel.Email, category, severity,
            deepLinkUrl, expiresAtUtc), ct);

    public Task QueueSmsAsync(
        Guid recipientUserId,
        string title,
        string body,
        NotificationCategory category,
        NotificationSeverity severity,
        DateTime? expiresAtUtc = null,
        CancellationToken ct = default) =>
        QueueAsync(new NotificationRequest(
            recipientUserId, title, body,
            NotificationChannel.Sms, category, severity,
            null, expiresAtUtc), ct);

    private static string BuildPayload(Notification n) =>
        JsonSerializer.Serialize(new
        {
            notificationId = n.Id,
            title = n.Title,
            body = n.Body,
            category = n.Category.ToString(),
            severity = n.Severity.ToString(),
            deepLinkUrl = n.DeepLinkUrl,
            expiresAtUtc = n.ExpiresAtUtc
        });
}
