using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class NotificationEntityTests
{
    private static Guid UserId() => Guid.NewGuid();

    // ── Notification.Create ───────────────────────────────────────────────────

    [Fact]
    public void Create_ValidArgs_SetsAllFields()
    {
        var userId = UserId();
        var expiry = DateTime.UtcNow.AddDays(1);

        var n = Notification.Create(
            userId, "Hello", "Body text",
            NotificationChannel.InApp,
            NotificationCategory.BillingUsage,
            NotificationSeverity.Warning,
            "/admin/ai-usage", expiry, "{\"key\":\"val\"}");

        n.Id.Should().NotBeEmpty();
        n.RecipientUserId.Should().Be(userId);
        n.Title.Should().Be("Hello");
        n.Body.Should().Be("Body text");
        n.Channel.Should().Be(NotificationChannel.InApp);
        n.Category.Should().Be(NotificationCategory.BillingUsage);
        n.Severity.Should().Be(NotificationSeverity.Warning);
        n.Status.Should().Be(NotificationStatus.Queued);
        n.DeepLinkUrl.Should().Be("/admin/ai-usage");
        n.ExpiresAtUtc.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));
        n.MetadataJson.Should().Be("{\"key\":\"val\"}");
        n.ReadAtUtc.Should().BeNull();
        n.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_EmptyRecipient_Throws()
    {
        var act = () => Notification.Create(
            Guid.Empty, "T", "B",
            NotificationChannel.InApp, NotificationCategory.System, NotificationSeverity.Info);

        act.Should().Throw<ArgumentException>().WithMessage("*RecipientUserId*");
    }

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        var act = () => Notification.Create(
            UserId(), "", "B",
            NotificationChannel.InApp, NotificationCategory.System, NotificationSeverity.Info);

        act.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    [Fact]
    public void Create_EmptyBody_Throws()
    {
        var act = () => Notification.Create(
            UserId(), "T", "   ",
            NotificationChannel.InApp, NotificationCategory.System, NotificationSeverity.Info);

        act.Should().Throw<ArgumentException>().WithMessage("*Body*");
    }

    // ── MarkRead ──────────────────────────────────────────────────────────────

    [Fact]
    public void MarkRead_SetsStatusAndTimestamp()
    {
        var n = Notification.Create(
            UserId(), "T", "B",
            NotificationChannel.InApp, NotificationCategory.System, NotificationSeverity.Info);

        n.MarkRead();

        n.Status.Should().Be(NotificationStatus.Read);
        n.ReadAtUtc.Should().NotBeNull();
        n.ReadAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkRead_Idempotent_DoesNotChangeTimestamp()
    {
        var n = Notification.Create(
            UserId(), "T", "B",
            NotificationChannel.InApp, NotificationCategory.System, NotificationSeverity.Info);
        n.MarkRead();
        var firstRead = n.ReadAtUtc;

        n.MarkRead();

        n.ReadAtUtc.Should().Be(firstRead);
    }

    // ── MarkDelivered ─────────────────────────────────────────────────────────

    [Fact]
    public void MarkDelivered_SetsDeliveredStatus()
    {
        var n = Notification.Create(
            UserId(), "T", "B",
            NotificationChannel.InApp, NotificationCategory.System, NotificationSeverity.Info);

        n.MarkDelivered();

        n.Status.Should().Be(NotificationStatus.Delivered);
    }

    [Fact]
    public void MarkDelivered_WhenAlreadyRead_DoesNotDowngrade()
    {
        var n = Notification.Create(
            UserId(), "T", "B",
            NotificationChannel.InApp, NotificationCategory.System, NotificationSeverity.Info);
        n.MarkRead();

        n.MarkDelivered();

        n.Status.Should().Be(NotificationStatus.Read);
    }

    // ── MarkFailed / Archive ──────────────────────────────────────────────────

    [Fact]
    public void MarkFailed_SetsFailedStatus()
    {
        var n = Notification.Create(
            UserId(), "T", "B",
            NotificationChannel.Email, NotificationCategory.Account, NotificationSeverity.Error);

        n.MarkFailed();

        n.Status.Should().Be(NotificationStatus.Failed);
    }

    [Fact]
    public void Archive_SetsArchivedStatus()
    {
        var n = Notification.Create(
            UserId(), "T", "B",
            NotificationChannel.InApp, NotificationCategory.System, NotificationSeverity.Info);

        n.Archive();

        n.Status.Should().Be(NotificationStatus.Archived);
    }

    // ── NotificationOutboxItem ────────────────────────────────────────────────

    [Fact]
    public void OutboxItem_Create_SetsAllFields()
    {
        var userId = UserId();
        var notifId = Guid.NewGuid();

        var item = NotificationOutboxItem.Create(
            userId, NotificationChannel.InApp, "{\"x\":1}", notifId);

        item.Id.Should().NotBeEmpty();
        item.RecipientUserId.Should().Be(userId);
        item.Channel.Should().Be(NotificationChannel.InApp);
        item.PayloadJson.Should().Be("{\"x\":1}");
        item.NotificationId.Should().Be(notifId);
        item.Status.Should().Be(NotificationStatus.Queued);
        item.AttemptCount.Should().Be(0);
        item.LastError.Should().BeNull();
        item.ProcessedAtUtc.Should().BeNull();
    }

    [Fact]
    public void OutboxItem_Create_EmptyRecipient_Throws()
    {
        var act = () => NotificationOutboxItem.Create(
            Guid.Empty, NotificationChannel.InApp, "{}");

        act.Should().Throw<ArgumentException>().WithMessage("*RecipientUserId*");
    }

    [Fact]
    public void OutboxItem_Create_EmptyPayload_Throws()
    {
        var act = () => NotificationOutboxItem.Create(
            Guid.NewGuid(), NotificationChannel.InApp, "");

        act.Should().Throw<ArgumentException>().WithMessage("*PayloadJson*");
    }

    [Fact]
    public void OutboxItem_RecordAttempt_Success_SetsDelivered()
    {
        var item = NotificationOutboxItem.Create(
            UserId(), NotificationChannel.InApp, "{}");

        item.RecordAttempt(true);

        item.Status.Should().Be(NotificationStatus.Delivered);
        item.AttemptCount.Should().Be(1);
        item.ProcessedAtUtc.Should().NotBeNull();
        item.LastError.Should().BeNull();
    }

    [Fact]
    public void OutboxItem_RecordAttempt_Failure_SetsFailed()
    {
        var item = NotificationOutboxItem.Create(
            UserId(), NotificationChannel.Email, "{}");

        item.RecordAttempt(false, "SMTP timeout");

        item.Status.Should().Be(NotificationStatus.Failed);
        item.AttemptCount.Should().Be(1);
        item.LastError.Should().Be("SMTP timeout");
        item.NextAttemptAtUtc.Should().NotBeNull();
        item.ProcessedAtUtc.Should().BeNull();
    }

    [Fact]
    public void OutboxItem_ResetForRetry_SetsQueued()
    {
        var item = NotificationOutboxItem.Create(
            UserId(), NotificationChannel.Email, "{}");
        item.RecordAttempt(false, "err");

        item.ResetForRetry();

        item.Status.Should().Be(NotificationStatus.Queued);
    }
}
