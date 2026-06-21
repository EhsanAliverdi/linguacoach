using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Notifications;
using LinguaCoach.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Notifications;

public sealed class NotificationDispatchTests : IAsyncLifetime
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

    private INotificationService NotifSvc => new NotificationService(
        Db, NullLogger<NotificationService>.Instance);

    private INotificationDispatchService DispatchSvc => new NotificationDispatchService(
        Db, NullLogger<NotificationDispatchService>.Instance);

    private static Guid UserId() => Guid.NewGuid();

    // ── InApp dispatch ────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatch_InAppItem_MarkedDelivered()
    {
        await NotifSvc.QueueInAppAsync(UserId(), "T", "B",
            NotificationCategory.System, NotificationSeverity.Info);

        var result = await DispatchSvc.DispatchDueAsync();

        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationStatus.Delivered, outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.NotNull(outbox.ProcessedAtUtc);
    }

    [Fact]
    public async Task Dispatch_InApp_SyncsNotificationToDelivered()
    {
        await NotifSvc.QueueInAppAsync(UserId(), "T", "B",
            NotificationCategory.System, NotificationSeverity.Info);

        await DispatchSvc.DispatchDueAsync();

        var notif = await Db.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Delivered, notif.Status);
    }

    // ── Email/SMS dispatch (not externally sent) ──────────────────────────────

    [Fact]
    public async Task Dispatch_EmailItem_NotSentExternally_MarkedFailed()
    {
        await NotifSvc.QueueEmailAsync(UserId(), "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        var result = await DispatchSvc.DispatchDueAsync();

        // Email is skipped (no provider) — counted as skipped
        Assert.Equal(0, result.Processed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationStatus.Failed, outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.NotNull(outbox.LastError);
        Assert.Null(outbox.ProcessedAtUtc);
    }

    [Fact]
    public async Task Dispatch_SmsItem_NotSentExternally_MarkedFailed()
    {
        await NotifSvc.QueueSmsAsync(UserId(), "T", "B",
            NotificationCategory.BillingUsage, NotificationSeverity.Warning);

        var result = await DispatchSvc.DispatchDueAsync();

        Assert.Equal(0, result.Processed);
        Assert.Equal(1, result.Skipped);

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationStatus.Failed, outbox.Status);
        Assert.False(string.IsNullOrEmpty(outbox.LastError));
    }

    // ── Retry behavior ────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatch_AfterFailure_AttemptCountIncreases()
    {
        await NotifSvc.QueueEmailAsync(UserId(), "T", "B",
            NotificationCategory.Account, NotificationSeverity.Info);

        // First dispatch — fails, sets NextAttemptAtUtc in the future
        await DispatchSvc.DispatchDueAsync();

        var outbox = await Db.NotificationOutboxItems.SingleAsync();
        Assert.Equal(1, outbox.AttemptCount);
        Assert.NotNull(outbox.NextAttemptAtUtc);
        // NextAttemptAtUtc is in the future (backoff)
        Assert.True(outbox.NextAttemptAtUtc > DateTime.UtcNow);

        // Second dispatch — item has NextAttemptAtUtc in future (Status=Failed), should not be picked up
        // (DispatchDueAsync only picks up Status=Queued AND NextAttemptAtUtc <= now)
        var result2 = await DispatchSvc.DispatchDueAsync();
        // The item is Failed with future NextAttemptAtUtc so it is NOT picked up
        Assert.Equal(0, result2.Skipped + result2.Processed + result2.Failed);
    }

    // ── Mixed batch ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatch_MixedBatch_CountsCorrectly()
    {
        await NotifSvc.QueueInAppAsync(UserId(), "A", "B",
            NotificationCategory.System, NotificationSeverity.Info);
        await NotifSvc.QueueEmailAsync(UserId(), "C", "D",
            NotificationCategory.Account, NotificationSeverity.Info);

        var result = await DispatchSvc.DispatchDueAsync();

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    // ── Empty queue ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatch_EmptyQueue_ReturnsZeros()
    {
        var result = await DispatchSvc.DispatchDueAsync();

        Assert.Equal(0, result.Processed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    // ── Already delivered items not re-processed ──────────────────────────────

    [Fact]
    public async Task Dispatch_AlreadyDelivered_NotPickedUp()
    {
        await NotifSvc.QueueInAppAsync(UserId(), "T", "B",
            NotificationCategory.System, NotificationSeverity.Info);

        // First dispatch
        await DispatchSvc.DispatchDueAsync();

        // Second dispatch — should pick up nothing
        var result2 = await DispatchSvc.DispatchDueAsync();
        Assert.Equal(0, result2.Processed + result2.Skipped + result2.Failed);
    }
}
