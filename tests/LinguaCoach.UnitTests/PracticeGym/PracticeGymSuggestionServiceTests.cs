using FluentAssertions;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.PracticeGym;
using LinguaCoach.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.PracticeGym;

/// <summary>
/// Unit tests for PracticeGymSuggestionService.
///
/// Phase I2A (legacy fallback deletion): SuggestedItems/ContinueItems/ReviewItems no longer
/// read the readiness pool for Practice-Gym-sourced rows — that generation path (including the
/// Phase 19C review/scaffold pilot gate and the Phase 20H dedupe logic previously covered here)
/// was removed. Those three lists are now always empty regardless of what's seeded in the pool.
/// StartSuggestionAsync/TryMarkConsumedAsync are unchanged (still operate generically on a given
/// readinessItemId) and remain covered below. See
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
/// </summary>
public sealed class PracticeGymSuggestionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly StubReplenishmentService _replenishment;
    private readonly PracticeGymSuggestionService _sut;

    private static readonly Guid StudentId = Guid.NewGuid();
    private readonly SqliteConnection _connection;

    public PracticeGymSuggestionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.EnsureCreated();
        _replenishment = new StubReplenishmentService { TargetCount = 10, ReadyCount = 0 };
        _sut = new PracticeGymSuggestionService(
            _db, _replenishment,
            new FallbackOnlyModuleSelectionService(), new NoOpModuleAssignmentRecorder(),
            NullLogger<PracticeGymSuggestionService>.Instance);
    }

    // Phase H7 — stubs so these tests are unaffected by the additive Practice Gym module
    // pipeline (always reports FallbackRequired, records nothing).
    private sealed class FallbackOnlyModuleSelectionService : IPracticeGymModuleSelectionService
    {
        public Task<PracticeGymModuleSelectionResult> SelectAsync(
            PracticeGymModuleSelectionRequest request, CancellationToken ct = default) =>
            Task.FromResult(new PracticeGymModuleSelectionResult([], true, "No modules configured for this test.", null, null, []));
    }

    private sealed class NoOpModuleAssignmentRecorder : IPracticeGymModuleAssignmentRecorder
    {
        public Task RecordAsync(Guid studentId, PracticeGymModuleSelectionResult selectionResult, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    // 1. SuggestedItems/ContinueItems/ReviewItems are always empty, regardless of pool contents.
    [Fact]
    public async Task GetSuggestions_ReadyItemInPool_DoesNotAppearInSuggestedItems()
    {
        SeedItem(status: ReadinessPoolStatus.Ready, routingReason: RoutingReason.Normal);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
        result.ContinueItems.Should().BeEmpty();
        result.ReviewItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuggestions_ReviewOnlyItemInPool_DoesNotAppearInReviewItems()
    {
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ReviewItems.Should().BeEmpty();
        result.SuggestedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuggestions_ReservedItemInPool_DoesNotAppearInContinueItems()
    {
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(2));
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ContinueItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuggestions_EmptyPool_ReplenishmentRecommended()
    {
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.IsReplenishmentRecommended.Should().BeTrue();
        result.SuggestedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuggestions_ReservedCount_IsAlwaysZero()
    {
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(2));
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ReservedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSuggestions_ModuleSuggestions_IsPopulatedFromModuleSelector()
    {
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ModuleSuggestions.Should().NotBeNull();
    }

    // StartSuggestionAsync/TryMarkConsumedAsync operate generically on a given readinessItemId
    // (no PracticeGym-source filter of their own) — unaffected by this pass.

    [Fact]
    public async Task StartSuggestion_ReservesReadyItem()
    {
        var activityId = Guid.NewGuid();
        var item = SeedItem(status: ReadinessPoolStatus.Ready, learningActivityId: activityId);

        var result = await _sut.StartSuggestionAsync(StudentId, item.Id);

        result.Success.Should().BeTrue();
        result.AlreadyReserved.Should().BeFalse();
        result.LearningActivityId.Should().Be(activityId);

        var dbItem = await _db.StudentActivityReadinessItems.FindAsync(item.Id);
        dbItem!.Status.Should().Be(ReadinessPoolStatus.Reserved);
    }

    [Fact]
    public async Task StartSuggestion_AlreadyReserved_ReturnsAlreadyReserved()
    {
        var activityId = Guid.NewGuid();
        var item = SeedItem(status: ReadinessPoolStatus.Reserved, learningActivityId: activityId);

        var result = await _sut.StartSuggestionAsync(StudentId, item.Id);

        result.Success.Should().BeTrue();
        result.AlreadyReserved.Should().BeTrue();
        result.LearningActivityId.Should().Be(activityId);
    }

    [Fact]
    public async Task StartSuggestion_ConsumedItem_ReturnsFailure()
    {
        var item = SeedItem(status: ReadinessPoolStatus.Consumed);

        var result = await _sut.StartSuggestionAsync(StudentId, item.Id);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TryMarkConsumed_ReservedItem_BecomesConsumed()
    {
        var item = SeedItem(status: ReadinessPoolStatus.Reserved);

        await _sut.TryMarkConsumedAsync(StudentId, item.Id);

        var dbItem = await _db.StudentActivityReadinessItems.FindAsync(item.Id);
        dbItem!.Status.Should().Be(ReadinessPoolStatus.Consumed);
    }

    // --- helpers ---

    private StudentActivityReadinessItem SeedItem(
        ReadinessPoolStatus status = ReadinessPoolStatus.Ready,
        RoutingReason routingReason = RoutingReason.Normal,
        bool isLower = false,
        DateTime? expiresAt = null,
        Guid? learningActivityId = null,
        bool requiresAdminReview = false,
        AdminReviewStatus? adminReviewStatus = null,
        Guid? studentId = null,
        string? curriculumObjectiveKey = null,
        string? patternKey = null)
    {
        var item = new StudentActivityReadinessItem(
            studentId: studentId ?? StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B2",
            routingReason: routingReason,
            isLowerLevelContent: isLower,
            curriculumObjectiveKey: curriculumObjectiveKey,
            patternKey: patternKey,
            expiresAt: expiresAt,
            requiresAdminReview: requiresAdminReview);

        ForceStatus(item, status);
        if (adminReviewStatus.HasValue) ForceAdminReviewStatus(item, adminReviewStatus.Value);
        if (learningActivityId.HasValue) ForceLinkedActivity(item, learningActivityId.Value);

        _db.StudentActivityReadinessItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private static void ForceStatus(StudentActivityReadinessItem item, ReadinessPoolStatus status) =>
        typeof(StudentActivityReadinessItem)
            .GetProperty(nameof(StudentActivityReadinessItem.Status))!
            .SetValue(item, status);

    private static void ForceAdminReviewStatus(StudentActivityReadinessItem item, AdminReviewStatus status) =>
        typeof(StudentActivityReadinessItem)
            .GetProperty(nameof(StudentActivityReadinessItem.AdminReviewStatus))!
            .SetValue(item, status);

    private static void ForceLinkedActivity(StudentActivityReadinessItem item, Guid activityId) =>
        typeof(StudentActivityReadinessItem)
            .GetProperty(nameof(StudentActivityReadinessItem.LearningActivityId))!
            .SetValue(item, activityId);

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

internal sealed class StubReplenishmentService : IReadinessPoolReplenishmentService
{
    public int TargetCount { get; set; } = 10;
    public int ReadyCount { get; set; }

    public Task<ReplenishmentRunSummary> RunAsync(CancellationToken ct = default) =>
        Task.FromResult(new ReplenishmentRunSummary());

    public Task<PoolHealthSummary> GetHealthAsync(
        Guid studentId, ReadinessPoolSource source, CancellationToken ct = default) =>
        Task.FromResult(new PoolHealthSummary
        {
            StudentId   = studentId,
            Source      = source,
            ReadyCount  = ReadyCount,
            TargetCount = TargetCount
        });
}
