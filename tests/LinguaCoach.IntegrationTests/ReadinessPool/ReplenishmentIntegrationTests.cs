using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Enums;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.ReadinessPool;

/// <summary>
/// Integration tests for Phase 10N readiness pool replenishment.
/// Verifies service registration, pool health, lifecycle recovery, and non-duplication.
/// </summary>
public sealed class ReplenishmentIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ReplenishmentIntegrationTests(ApiTestFactory factory)
        => _factory = factory;

    // 1. IReadinessPoolReplenishmentService is registered and resolves.
    [Fact]
    public void ReplenishmentService_IsRegisteredInDI()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IReadinessPoolReplenishmentService>();
        Assert.NotNull(svc);
    }

    // 2. PoolHealthSummary counts only usable (ready) items toward target.
    [Fact]
    public async Task GetHealth_CountsOnlyReadyItems_TowardTarget()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var studentId = Guid.NewGuid();

        // Create a ready item.
        var readyId = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(readyId);
        await poolSvc.MarkReadyAsync(readyId);

        // Create a failed item (should not count).
        var failedId = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(failedId);
        await poolSvc.MarkFailedAsync(failedId, "ERR", "generation failed");

        // Create a stale item (should not count).
        var staleId = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(staleId);
        await poolSvc.MarkReadyAsync(staleId);
        await poolSvc.MarkStaleAsync(staleId, "profile changed");

        var health = await replenish.GetHealthAsync(studentId, ReadinessPoolSource.TodayLesson);

        // Only the one ready item counts.
        Assert.Equal(1, health.ReadyCount);
        Assert.Equal(1, health.FailedCount);
        Assert.Equal(1, health.StaleCount);
        Assert.True(health.NeedsReplenishment);
    }

    // 3. ReviewOnly items do not count toward normal target shortfall.
    [Fact]
    public async Task GetHealth_ReviewOnly_NotCountedTowardNormalTarget()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var studentId = Guid.NewGuid();

        var id = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.PracticeGym));
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);
        await poolSvc.MarkReviewOnlyAsync(id, "passed objective");

        var health = await replenish.GetHealthAsync(studentId, ReadinessPoolSource.PracticeGym);

        Assert.Equal(0, health.ReadyCount);
        Assert.Equal(1, health.ReviewOnlyCount);
        // Pool needs replenishment because ReviewOnly doesn't fill normal shortfall.
        Assert.True(health.NeedsReplenishment);
    }

    // 4. Expired items are not counted toward ready.
    [Fact]
    public async Task GetHealth_ExpiredItems_NotCountedTowardReady()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var studentId = Guid.NewGuid();

        var id = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.PracticeGym));
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);
        await poolSvc.ExpireAsync(id, "test expiry");

        var health = await replenish.GetHealthAsync(studentId, ReadinessPoolSource.PracticeGym);

        Assert.Equal(0, health.ReadyCount);
        Assert.Equal(1, health.ExpiredCount);
        Assert.True(health.NeedsReplenishment);
    }

    // 5. Queued items count toward in-flight (prevent over-generation).
    [Fact]
    public async Task GetHealth_QueuedAndGenerating_CountTowardInFlight()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var studentId = Guid.NewGuid();

        // Create 2 queued + 1 generating.
        var q1 = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        var q2 = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        var g1 = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(g1);

        var health = await replenish.GetHealthAsync(studentId, ReadinessPoolSource.TodayLesson);

        Assert.Equal(0, health.ReadyCount);
        Assert.Equal(3, health.QueuedOrGeneratingCount);
    }

    // 6. Failed items can be queued for retry via pool service (simulates retry path).
    [Fact]
    public async Task FailedItem_CanBeRetried_ByCreatingNewQueuedItem()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();

        // Create + fail an item.
        var id = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.PracticeGym));
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkFailedAsync(id, "TIMEOUT", "orphaned generating");

        // Retry = create a new queued item from the same snapshot.
        var retryId = await poolSvc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = studentId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false,
            ContextTagsJson = "[\"general_english\"]",
            GeneratedBy = "ReadinessPoolReplenishment:Retry"
        });

        Assert.NotEqual(id, retryId);

        var health = await poolSvc.GetPoolSummaryAsync(studentId);
        Assert.Equal(1, health.QueuedCount);
        Assert.Equal(1, health.FailedCount);
    }

    // 7. Admin health endpoint responds 200 for known student.
    [Fact]
    public async Task AdminHealthEndpoint_Returns200_WithHealthSummary()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var studentId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/admin/students/{studentId}/readiness-pool/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("todayLesson", body);
        Assert.Contains("practiceGym", body);
        Assert.Contains("targetCount", body);
    }

    // 8. Admin pool endpoint still returns summary (not broken by Phase 10N).
    [Fact]
    public async Task AdminPoolEndpoint_Returns200_WithSummary()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var studentId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/admin/students/{studentId}/readiness-pool");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("readyCount", body);
    }

    // 9. Existing Phase 10M lifecycle tests still work (smoke).
    [Fact]
    public async Task ExistingPool_QueuedToReady_SmokeTest()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var studentId = Guid.NewGuid();
        var id = await svc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.LessonBatch));
        await svc.MarkGeneratingAsync(id);
        var activityId = Guid.NewGuid();
        await svc.MarkReadyAsync(id, learningActivityId: activityId);

        db.ChangeTracker.Clear();
        var item = await db.StudentActivityReadinessItems.FindAsync(id);
        Assert.NotNull(item);
        Assert.Equal(ReadinessPoolStatus.Ready, item.Status);
        Assert.Equal(activityId, item.LearningActivityId);
    }

    // 10. GetHealth returns zero counts for unknown student (not an error).
    [Fact]
    public async Task GetHealth_UnknownStudent_ReturnsZeroCounts()
    {
        using var scope = _factory.Services.CreateScope();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var health = await replenish.GetHealthAsync(Guid.NewGuid(), ReadinessPoolSource.TodayLesson);

        Assert.Equal(0, health.ReadyCount);
        Assert.Equal(0, health.QueuedOrGeneratingCount);
        Assert.Equal(0, health.FailedCount);
        Assert.True(health.NeedsReplenishment);
    }

    // 11. Two different students' health is isolated.
    [Fact]
    public async Task GetHealth_TwoStudents_IsIsolated()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();

        var id1 = await poolSvc.CreateQueuedAsync(MakeRequest(s1, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(id1);
        await poolSvc.MarkReadyAsync(id1);

        var h1 = await replenish.GetHealthAsync(s1, ReadinessPoolSource.TodayLesson);
        var h2 = await replenish.GetHealthAsync(s2, ReadinessPoolSource.TodayLesson);

        Assert.Equal(1, h1.ReadyCount);
        Assert.Equal(0, h2.ReadyCount);
    }

    // 12. ReservedCount is populated in pool health summary (Phase 10Y).
    [Fact]
    public async Task GetHealth_ReservedItem_CountedInReservedCount()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var studentId = Guid.NewGuid();

        var id = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.PracticeGym));
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);
        await poolSvc.ReserveNextReadyAsync(studentId, ReadinessPoolSource.PracticeGym);

        var health = await replenish.GetHealthAsync(studentId, ReadinessPoolSource.PracticeGym);

        Assert.Equal(0, health.ReadyCount);
        Assert.Equal(1, health.ReservedCount);
    }

    // 13. SkippedCount is populated in pool health summary (Phase 10Y).
    [Fact]
    public async Task GetHealth_SkippedItem_CountedInSkippedCount()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var studentId = Guid.NewGuid();

        var id = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);
        await poolSvc.MarkSkippedAsync(id, "mastered");

        var health = await replenish.GetHealthAsync(studentId, ReadinessPoolSource.TodayLesson);

        Assert.Equal(0, health.ReadyCount);
        Assert.Equal(1, health.SkippedCount);
        Assert.True(health.NeedsReplenishment);
    }

    // 14. MarkSkippedAsync persists Skipped status to DB (Phase 10Y).
    [Fact]
    public async Task MarkSkippedAsync_PersistsSkippedStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var studentId = Guid.NewGuid();
        var id = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.PracticeGym));
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);
        await poolSvc.MarkSkippedAsync(id, "no longer relevant");

        db.ChangeTracker.Clear();
        var item = await db.StudentActivityReadinessItems.FindAsync(id);
        Assert.NotNull(item);
        Assert.Equal(ReadinessPoolStatus.Skipped, item.Status);
        Assert.Equal("no longer relevant", item.ErrorMessage);
    }

    // 15. Skipped item does not appear in GetReadyForStudentAsync (Phase 10Y).
    [Fact]
    public async Task SkippedItem_NotReturnedByGetReadyForStudent()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var studentId = Guid.NewGuid();
        var id = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);
        await poolSvc.MarkSkippedAsync(id, "mastered");

        var ready = await poolSvc.GetReadyForStudentAsync(studentId, ReadinessPoolSource.TodayLesson);
        Assert.Empty(ready);
    }

    // 16. Admin health endpoint now includes reservedCount and skippedCount fields (Phase 10Y).
    [Fact]
    public async Task AdminHealthEndpoint_IncludesReservedAndSkippedCounts()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var studentId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/admin/students/{studentId}/readiness-pool/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("reservedCount", body);
        Assert.Contains("skippedCount", body);
    }

    // Phase 12C: AggregatePoolHealth endpoint returns new derived fields.
    [Fact]
    public async Task AggregatePoolHealth_IncludesNewDerivedFields()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/readiness-pool/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("studentsBelowMinimumThreshold", body);
        Assert.Contains("averageReadyPerStudent", body);
    }

    // Phase 12C: GetHealth counts Reserved separately from Ready.
    [Fact]
    public async Task GetHealth_ReservedItems_CountedSeparately()
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var replenish = scope.ServiceProvider.GetRequiredService<IReadinessPoolReplenishmentService>();

        var studentId = Guid.NewGuid();

        var id1 = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(id1);
        await poolSvc.MarkReadyAsync(id1);

        var id2 = await poolSvc.CreateQueuedAsync(MakeRequest(studentId, ReadinessPoolSource.TodayLesson));
        await poolSvc.MarkGeneratingAsync(id2);
        await poolSvc.MarkReadyAsync(id2);
        await poolSvc.ReserveNextReadyAsync(studentId, ReadinessPoolSource.TodayLesson);

        var health = await replenish.GetHealthAsync(studentId, ReadinessPoolSource.TodayLesson);

        Assert.Equal(1, health.ReadyCount);
        Assert.Equal(1, health.ReservedCount);
    }

    // Phase 12C: ReplenishmentRunSummary SkippedAtMaxBuffer defaults to zero.
    [Fact]
    public void ReplenishmentRunSummary_SkippedAtMaxBuffer_DefaultsToZero()
    {
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        Assert.Equal(0, summary.SkippedAtMaxBuffer);
        Assert.Equal(1.0, summary.GenerationSuccessRate);
    }

    // Phase 12C: ElapsedMs is correct for a known duration.
    [Fact]
    public void ReplenishmentRunSummary_ElapsedMs_IsCorrect()
    {
        var start = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc);
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = start,
            CompletedAt = start.AddMilliseconds(250)
        };
        Assert.Equal(250L, summary.ElapsedMs);
    }

    // Phase 12C: MinimumReadyThreshold and MaxBufferCount have valid defaults from DI.
    [Fact]
    public void ReplenishmentOptions_Phase12C_NewDefaults_AreValid()
    {
        using var scope = _factory.Services.CreateScope();
        var opts = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<ReadinessPoolReplenishmentOptions>>()
            .Value;

        Assert.True(opts.MinimumReadyThreshold > 0);
        Assert.True(opts.MaxBufferCount > 0);
        Assert.True(opts.MaxBufferCount >= opts.TodayLessonPoolTargetCount);
    }

    private static CreateReadinessItemRequest MakeRequest(
        Guid studentId,
        ReadinessPoolSource source,
        string cefr = "B2") => new()
    {
        StudentId = studentId,
        Source = source,
        TargetCefrLevel = cefr,
        RoutingReason = RoutingReason.Normal,
        IsLowerLevelContent = false,
        ContextTagsJson = "[\"general_english\"]",
        GeneratedBy = "replenishment-integration-test"
    };
}
