using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

public sealed class Notification : BaseEntity
{
    public Guid RecipientUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationCategory Category { get; private set; }
    public NotificationSeverity Severity { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string? DeepLinkUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public string? MetadataJson { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid recipientUserId,
        string title,
        string body,
        NotificationChannel channel,
        NotificationCategory category,
        NotificationSeverity severity,
        string? deepLinkUrl = null,
        DateTime? expiresAtUtc = null,
        string? metadataJson = null)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("RecipientUserId is required.", nameof(recipientUserId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body is required.", nameof(body));

        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Title = title.Trim(),
            Body = body.Trim(),
            Channel = channel,
            Category = category,
            Severity = severity,
            Status = NotificationStatus.Queued,
            DeepLinkUrl = deepLinkUrl,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            MetadataJson = metadataJson
        };
    }

    public void MarkRead()
    {
        if (Status == NotificationStatus.Read) return;
        Status = NotificationStatus.Read;
        ReadAtUtc = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        if (Status is NotificationStatus.Delivered or NotificationStatus.Read) return;
        Status = NotificationStatus.Delivered;
    }

    public void MarkFailed()
    {
        Status = NotificationStatus.Failed;
    }

    public void Archive()
    {
        Status = NotificationStatus.Archived;
    }
}
