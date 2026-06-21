using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Notifications;
using LinguaCoach.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Notifications;

public sealed class NotificationServiceTests : IAsyncLifetime
{
    private SqliteConnection? _connection;
    private LinguaCoachDbContext? _db;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LinguaCoachDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_db is not null) await _db.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private LinguaCoachDbContext Db => _db!;

    private INotificationService Svc => new NotificationService(
        Db, new NotificationPreferenceService(Db), NullLogger<NotificationService>.Instance);

    private static Guid UserId() => Guid.NewGuid();

    // ── QueueInAppAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task QueueInApp_CreatesNotificationRow()
    {
        var userId = UserId();

        await Svc.QueueInAppAsync(userId, "Test title", "Test body",
            NotificationCategory.BillingUsage, NotificationSeverity.Warning);

        var notif = await Db.Notifications.SingleAsync();
        Assert.Equal(userId, notif.RecipientUserId);
        Assert.Equal("Test title", notif.Title);
        Assert.Equal("Test body", notif.Body);
        Assert.Equal(NotificationChannel.InApp, notif.Channel);
        Assert.Equal(NotificationCategory.BillingUsage, notif.Category);
        Assert.Equal(NotificationSeverity.Warning, notif.Severity);
    }

    [Fact]
    public async Task QueueInApp_StartsWithQueuedStatus()
    {
        await Svc.QueueInAppAsync(UserId(), "T", "B",
            NotificationCategory.System, NotificationSeverity.Info);

        var notif = await Db.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Queued, notif.Status);
        Assert.Null(notif.ReadAtUtc);
    }

    [Fact]
    public async Task QueueInApp_CreatesOutboxItem()
    {
        var userId = UserId();

        await Svc.QueueInAppAsync(userId, "T", "B",
            NotificationCategory.System, NotificationSeverity.Info);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(userId, outbox.RecipientUserId);
        Assert.Equal(NotificationChannel.InApp, outbox.Channel);
        Assert.Equal(NotificationStatus.Queued, outbox.Status);
        Assert.Equal(0, outbox.AttemptCount);
        Assert.False(string.IsNullOrEmpty(outbox.PayloadJson));
    }

    [Fact]
    public async Task QueueInApp_OutboxItemLinkedToNotification()
    {
        await Svc.QueueInAppAsync(UserId(), "T", "B",
            NotificationCategory.System, NotificationSeverity.Info);

        var notif = await Db.Notifications.SingleAsync();
        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(notif.Id, outbox.NotificationId);
    }

    [Fact]
    public async Task QueueInApp_StoresDeepLinkAndExpiry()
    {
        var expiry = DateTime.UtcNow.AddDays(7);

        await Svc.QueueInAppAsync(UserId(), "T", "B",
            NotificationCategory.Admin, NotificationSeverity.Error,
            "/admin/ai-usage", expiry);

        var notif = await Db.Notifications.SingleAsync();
        Assert.Equal("/admin/ai-usage", notif.DeepLinkUrl);
        Assert.NotNull(notif.ExpiresAtUtc);
        Assert.True(Math.Abs((notif.ExpiresAtUtc!.Value - expiry).TotalSeconds) < 5);
    }

    [Fact]
    public async Task QueueInApp_StoresCategoryAndSeverity()
    {
        await Svc.QueueInAppAsync(UserId(), "T", "B",
            NotificationCategory.BackgroundJob, NotificationSeverity.Error);

        var notif = await Db.Notifications.SingleAsync();
        Assert.Equal(NotificationCategory.BackgroundJob, notif.Category);
        Assert.Equal(NotificationSeverity.Error, notif.Severity);
    }

    // ── QueueEmailAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task QueueEmail_CreatesNotificationAndOutboxItem()
    {
        var userId = UserId();

        await Svc.QueueEmailAsync(userId, "Reset password", "Click here",
            NotificationCategory.Account, NotificationSeverity.Info);

        var notif = await Db.Notifications.SingleAsync();
        Assert.Equal(NotificationChannel.Email, notif.Channel);
        Assert.Equal(NotificationStatus.Queued, notif.Status);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationChannel.Email, outbox.Channel);
    }

    [Fact]
    public async Task QueueEmail_NoDeliveryAttempts_InThisPhase()
    {
        await Svc.QueueEmailAsync(UserId(), "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(0, outbox.AttemptCount);
        Assert.Null(outbox.ProcessedAtUtc);
        Assert.Null(outbox.LastAttemptAtUtc);
    }

    // ── QueueSmsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task QueueSms_IsSkippedByPreferenceLayer_SmsDeferred()
    {
        // SMS is permanently disabled via INotificationPreferenceService.
        // QueueSmsAsync should silently skip — no notification or outbox row created.
        var userId = UserId();

        await Svc.QueueSmsAsync(userId, "Quota warning", "80% used",
            NotificationCategory.BillingUsage, NotificationSeverity.Warning);

        Assert.Equal(0, await Db.Notifications.CountAsync());
        Assert.Equal(0, await Db.NotificationOutboxItems.CountAsync());
    }

    [Fact]
    public async Task QueueSms_NoOutboxItem_WhenDeferred()
    {
        // SMS deferred: no outbox row is written.
        await Svc.QueueSmsAsync(UserId(), "T", "B",
            NotificationCategory.BillingUsage, NotificationSeverity.Info);

        Assert.Equal(0, await Db.NotificationOutboxItems.CountAsync());
    }

    // ── QueueAsync (generic) ──────────────────────────────────────────────────

    [Fact]
    public async Task QueueAsync_WithMetadata_StoresMetadataJson()
    {
        var request = new NotificationRequest(
            UserId(), "T", "B",
            NotificationChannel.InApp,
            NotificationCategory.System,
            NotificationSeverity.Info,
            MetadataJson: "{\"alertType\":\"zero_cost\"}");

        await Svc.QueueAsync(request);

        var notif = await Db.Notifications.SingleAsync();
        Assert.Equal("{\"alertType\":\"zero_cost\"}", notif.MetadataJson);
    }

    // ── Multiple queued items ─────────────────────────────────────────────────

    [Fact]
    public async Task QueueMultiple_EachCreatesOwnRows()
    {
        var userId = UserId();

        await Svc.QueueInAppAsync(userId, "A", "B1",
            NotificationCategory.System, NotificationSeverity.Info);
        await Svc.QueueEmailAsync(userId, "B", "B2",
            NotificationCategory.Account, NotificationSeverity.Info);

        Assert.Equal(2, Db.Notifications.Count());
        Assert.Equal(2, Db.NotificationOutboxItems.Count());
    }
}
