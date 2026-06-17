using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Enums;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.ReadinessPool;

/// <summary>
/// Integration tests for Phase 10M student activity readiness pool.
/// Uses the shared SQLite in-memory DB via ApiTestFactory.
/// </summary>
public sealed class ReadinessPoolIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ReadinessPoolIntegrationTests(ApiTestFactory factory)
        => _factory = factory;

    // 1. Service is registered and resolves.
    [Fact]
    public void PoolService_IsRegisteredInDI()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IStudentActivityReadinessPoolService>();
        Assert.NotNull(svc);
    }

    // 2. EF can persist and reload a readiness item.
    [Fact]
    public async Task CreateQueued_Persists_AndCanBeReloaded()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var studentId = Guid.NewGuid();
        var req = new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false,
            ContextTagsJson = "[\"general_english\"]",
            GeneratedBy = "test"
        };

        var id = await svc.CreateQueuedAsync(req);

        var loaded = await db.StudentActivityReadinessItems.FindAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal(ReadinessPoolStatus.Queued, loaded.Status);
        Assert.Equal(studentId, loaded.StudentId);
        Assert.Equal("B2", loaded.TargetCefrLevel);
        Assert.Contains("general_english", loaded.ContextTagsJson);
    }

    // 3. Full lifecycle queued→generating→ready persists correctly.
    [Fact]
    public async Task Lifecycle_QueuedToReady_PersistsAllStates()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var studentId = Guid.NewGuid();
        var id = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.LessonBatch,
            TargetCefrLevel = "B1",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });

        await svc.MarkGeneratingAsync(id);
        var activityId = Guid.NewGuid();
        await svc.MarkReadyAsync(id, learningActivityId: activityId);

        db.ChangeTracker.Clear();
        var item = await db.StudentActivityReadinessItems.FindAsync(id);
        Assert.NotNull(item);
        Assert.Equal(ReadinessPoolStatus.Ready, item.Status);
        Assert.Equal(activityId, item.LearningActivityId);
        Assert.Equal(1, item.AttemptCount);
    }

    // 4. Reservation is safe — only one active reservation per item.
    [Fact]
    public async Task Reservation_IsSafe_OnlyOneActiveReservation()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();
        var id = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(id);
        await svc.MarkReadyAsync(id);

        var first = await svc.ReserveNextReadyAsync(studentId, ReadinessPoolSource.PracticeGym);
        Assert.NotNull(first);
        Assert.Equal(ReadinessPoolStatus.Reserved, first.Status);

        // No more ready items — second call must return null.
        var second = await svc.ReserveNextReadyAsync(studentId, ReadinessPoolSource.PracticeGym);
        Assert.Null(second);
    }

    // 5. Consumed item cannot be reserved.
    [Fact]
    public async Task Consumed_Item_NotReturnedByReserve()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();
        var id = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(id);
        await svc.MarkReadyAsync(id);
        var reserved = await svc.ReserveNextReadyAsync(studentId, ReadinessPoolSource.PracticeGym);
        Assert.NotNull(reserved);
        await svc.MarkConsumedAsync(reserved.Id);

        var next = await svc.ReserveNextReadyAsync(studentId, ReadinessPoolSource.PracticeGym);
        Assert.Null(next);
    }

    // 6. Expired item not returned by GetReadyForStudentAsync.
    [Fact]
    public async Task Expired_Item_NotReturnedByGetReady()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();
        var id = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(id);
        await svc.MarkReadyAsync(id);
        await svc.ExpireAsync(id, "test expiry");

        var ready = await svc.GetReadyForStudentAsync(studentId);
        Assert.DoesNotContain(ready, i => i.Id == id);
    }

    // 7. Stale item not served as normal ready.
    [Fact]
    public async Task Stale_Item_NotReturnedByGetReady()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();
        var id = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.LessonBatch,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(id);
        await svc.MarkReadyAsync(id);
        await svc.MarkStaleAsync(id, "profile changed");

        var ready = await svc.GetReadyForStudentAsync(studentId);
        Assert.DoesNotContain(ready, i => i.Id == id);
    }

    // 8. ReviewOnly item excluded from normal ready query.
    [Fact]
    public async Task ReviewOnly_Item_NotReturnedByGetReady()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();
        var id = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(id);
        await svc.MarkReadyAsync(id);
        await svc.MarkReviewOnlyAsync(id, "passed objective");

        var ready = await svc.GetReadyForStudentAsync(studentId, ReadinessPoolSource.PracticeGym);
        Assert.DoesNotContain(ready, i => i.Id == id);
    }

    // 9. Pool summary counts by status.
    [Fact]
    public async Task GetPoolSummary_ReturnsCorrectCounts()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();

        await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });

        var id2 = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.LessonBatch,
            TargetCefrLevel = "B1",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(id2);
        await svc.MarkReadyAsync(id2);

        var summary = await svc.GetPoolSummaryAsync(studentId);
        Assert.Equal(1, summary.QueuedCount);
        Assert.Equal(1, summary.ReadyCount);
        Assert.Equal(studentId, summary.StudentId);
    }

    // 10. Query by student/status/source returns correct subset.
    [Fact]
    public async Task Query_ByStudentStatusSource_ReturnsCorrectSubset()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var gymId = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(gymId);
        await svc.MarkReadyAsync(gymId);

        var batchId = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.LessonBatch,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(batchId);
        await svc.MarkReadyAsync(batchId);

        // Other student — still queued, not ready.
        await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = otherId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });

        var gymItems = await svc.GetReadyForStudentAsync(studentId, ReadinessPoolSource.PracticeGym);
        Assert.Single(gymItems);
        Assert.Equal(ReadinessPoolSource.PracticeGym, gymItems[0].Source);

        var allItems = await svc.GetReadyForStudentAsync(studentId);
        Assert.Equal(2, allItems.Count);

        var otherItems = await svc.GetReadyForStudentAsync(otherId);
        Assert.Empty(otherItems);
    }

    // 11. LinkMaterializedIds updates linked ids.
    [Fact]
    public async Task LinkMaterializedIds_UpdatesLinkedIds()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var studentId = Guid.NewGuid();
        var id = await svc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.LessonBatch,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false
        });
        await svc.MarkGeneratingAsync(id);
        await svc.MarkReadyAsync(id);

        var sessionId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        await svc.LinkMaterializedIdsAsync(id, sessionId, activityId, null);

        db.ChangeTracker.Clear();
        var item = await db.StudentActivityReadinessItems.FindAsync(id);
        Assert.NotNull(item);
        Assert.Equal(sessionId, item.LearningSessionId);
        Assert.Equal(activityId, item.LearningActivityId);
    }
}
