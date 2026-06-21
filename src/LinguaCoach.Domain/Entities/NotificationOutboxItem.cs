using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

public sealed class NotificationOutboxItem : BaseEntity
{
    public Guid? NotificationId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    private NotificationOutboxItem() { }

    public static NotificationOutboxItem Create(
        Guid recipientUserId,
        NotificationChannel channel,
        string payloadJson,
        Guid? notificationId = null)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("RecipientUserId is required.", nameof(recipientUserId));
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new ArgumentException("PayloadJson is required.", nameof(payloadJson));

        return new NotificationOutboxItem
        {
            Id = Guid.NewGuid(),
            NotificationId = notificationId,
            RecipientUserId = recipientUserId,
            Channel = channel,
            PayloadJson = payloadJson,
            Status = NotificationStatus.Queued,
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow
        };
    }

    public void RecordAttempt(bool success, string? error = null)
    {
        AttemptCount++;
        LastAttemptAtUtc = DateTime.UtcNow;

        if (success)
        {
            Status = NotificationStatus.Delivered;
            ProcessedAtUtc = DateTime.UtcNow;
            LastError = null;
        }
        else
        {
            Status = NotificationStatus.Failed;
            LastError = error;
            NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(5 * AttemptCount);
        }
    }

    public void ResetForRetry()
    {
        Status = NotificationStatus.Queued;
        NextAttemptAtUtc = DateTime.UtcNow;
    }
}
