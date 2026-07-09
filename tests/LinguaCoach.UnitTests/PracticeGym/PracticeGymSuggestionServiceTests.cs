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
/// Unit tests for PracticeGymSuggestionService — Phase 10O, extended in Phase 19C for the
/// Practice Gym review scaffold pilot gate.
/// Uses an in-memory database and a stub replenishment service.
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
        _sut = BuildSut(new ReadinessPoolReplenishmentOptions());
    }

    private PracticeGymSuggestionService BuildSut(ReadinessPoolReplenishmentOptions opts) =>
        new(_db, _replenishment, new StubSettingsProvider(opts),
            new FallbackOnlyModuleSelectionService(), new NoOpModuleAssignmentRecorder(),
            NullLogger<PracticeGymSuggestionService>.Instance);

    // Phase H7 — stubs so these pre-existing readiness-pool tests are unaffected by the new,
    // additive Practice Gym module pipeline (always reports FallbackRequired, records nothing).
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

    // 1. Consumed items excluded from all sections.
    [Fact]
    public async Task GetSuggestions_ExcludesConsumedItems()
    {
        SeedItem(status: ReadinessPoolStatus.Consumed);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
        result.ContinueItems.Should().BeEmpty();
        result.ReviewItems.Should().BeEmpty();
    }

    // 2. Expired items excluded.
    [Fact]
    public async Task GetSuggestions_ExcludesExpiredItems()
    {
        SeedItem(status: ReadinessPoolStatus.Expired);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 3. Failed items excluded.
    [Fact]
    public async Task GetSuggestions_ExcludesFailedItems()
    {
        SeedItem(status: ReadinessPoolStatus.Failed);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 4. Stale items excluded.
    [Fact]
    public async Task GetSuggestions_ExcludesStaleItems()
    {
        SeedItem(status: ReadinessPoolStatus.Stale);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 5. ReviewOnly status item appears in Review section only, not Suggested.
    [Fact]
    public async Task GetSuggestions_ReviewOnlyAppearsInReviewSectionOnly()
    {
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ReviewItems.Should().HaveCount(1);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 6. Lower-level Ready+Review items appear in Review, not Suggested.
    [Fact]
    public async Task GetSuggestions_LowerLevelReadyReviewAppearsInReviewOnly()
    {
        SeedItem(status: ReadinessPoolStatus.Ready, routingReason: RoutingReason.Review, isLower: true);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ReviewItems.Should().HaveCount(1);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 7. Normal Ready item appears in Suggested section.
    [Fact]
    public async Task GetSuggestions_NormalReadyItemAppearsInSuggested()
    {
        SeedItem(status: ReadinessPoolStatus.Ready, routingReason: RoutingReason.Normal, isLower: false);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().HaveCount(1);
        result.ReviewItems.Should().BeEmpty();
    }

    // 8. Reserved valid item appears in Continue section.
    [Fact]
    public async Task GetSuggestions_ReservedValidItemInContinueSection()
    {
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(2));
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ContinueItems.Should().HaveCount(1);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 9. Reserved item with past ExpiresAt NOT in Continue.
    [Fact]
    public async Task GetSuggestions_ExpiredReservedItemNotInContinue()
    {
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(-1));
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ContinueItems.Should().BeEmpty();
    }

    // 10. Empty pool → IsReplenishmentRecommended = true.
    [Fact]
    public async Task GetSuggestions_EmptyPool_ReplenishmentRecommended()
    {
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.IsReplenishmentRecommended.Should().BeTrue();
        result.SuggestedItems.Should().BeEmpty();
    }

    // 11. StartSuggestion reserves a Ready item and returns linked activity id.
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

    // 12. StartSuggestion is idempotent for already-reserved items.
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

    // 13. StartSuggestion returns failure for consumed item (not available).
    [Fact]
    public async Task StartSuggestion_ConsumedItem_ReturnsFailure()
    {
        var item = SeedItem(status: ReadinessPoolStatus.Consumed);

        var result = await _sut.StartSuggestionAsync(StudentId, item.Id);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrEmpty();
    }

    // 14. TryMarkConsumed transitions Reserved → Consumed.
    [Fact]
    public async Task TryMarkConsumed_ReservedItem_BecomesConsumed()
    {
        var item = SeedItem(status: ReadinessPoolStatus.Reserved);

        await _sut.TryMarkConsumedAsync(StudentId, item.Id);

        var dbItem = await _db.StudentActivityReadinessItems.FindAsync(item.Id);
        dbItem!.Status.Should().Be(ReadinessPoolStatus.Consumed);
    }

    // 15. Ready item with RequiresAdminReview=true is excluded from Suggested.
    [Fact]
    public async Task GetSuggestions_RequiresAdminReview_ExcludedFromSuggested()
    {
        SeedItem(status: ReadinessPoolStatus.Ready, routingReason: RoutingReason.Normal, requiresAdminReview: true);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 16. ReviewOnly item with RequiresAdminReview=true is excluded from Review section too.
    [Fact]
    public async Task GetSuggestions_RequiresAdminReview_ExcludedFromReviewSection()
    {
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true, requiresAdminReview: true);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ReviewItems.Should().BeEmpty();
    }

    // 17. Reserved item with RequiresAdminReview=true is excluded from Continue.
    [Fact]
    public async Task GetSuggestions_RequiresAdminReview_ExcludedFromContinue()
    {
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(2), requiresAdminReview: true);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ContinueItems.Should().BeEmpty();
    }

    // 18. Same item without RequiresAdminReview appears normally (control case for #15).
    [Fact]
    public async Task GetSuggestions_WithoutRequiresAdminReview_AppearsInSuggested()
    {
        SeedItem(status: ReadinessPoolStatus.Ready, routingReason: RoutingReason.Normal, requiresAdminReview: false);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().HaveCount(1);
    }

    // --- Phase 19C: Practice Gym review scaffold pilot gate ---

    // 19. Approved scaffold item hidden when pilot disabled (default).
    [Fact]
    public async Task GetSuggestions_PilotDisabled_ApprovedScaffoldItemHidden()
    {
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true,
            requiresAdminReview: true, adminReviewStatus: AdminReviewStatus.Approved);

        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);

        result.ReviewItems.Should().BeEmpty();
    }

    // 20. Approved scaffold item visible when pilot enabled and all gates pass.
    [Fact]
    public async Task GetSuggestions_PilotEnabled_ApprovedScaffoldItemVisible()
    {
        var sut = BuildSut(new ReadinessPoolReplenishmentOptions { PracticeGymPilotEnabled = true });
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true,
            requiresAdminReview: true, adminReviewStatus: AdminReviewStatus.Approved);

        var result = await sut.GetSuggestionsForStudentAsync(StudentId);

        result.ReviewItems.Should().HaveCount(1);
        result.ReviewItems[0].CallToAction.Should().Be("Review");
        result.ReviewItems[0].Explanation.Should().Be("This helps you practise a skill you are building.");
    }

    // 21. Pending review scaffold item hidden even when pilot enabled.
    [Fact]
    public async Task GetSuggestions_PilotEnabled_PendingReviewItemHidden()
    {
        var sut = BuildSut(new ReadinessPoolReplenishmentOptions { PracticeGymPilotEnabled = true });
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true,
            requiresAdminReview: true, adminReviewStatus: AdminReviewStatus.PendingReview);

        var result = await sut.GetSuggestionsForStudentAsync(StudentId);

        result.ReviewItems.Should().BeEmpty();
    }

    // 22. Rejected review scaffold item hidden even when pilot enabled.
    [Fact]
    public async Task GetSuggestions_PilotEnabled_RejectedItemHidden()
    {
        var sut = BuildSut(new ReadinessPoolReplenishmentOptions { PracticeGymPilotEnabled = true });
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true,
            requiresAdminReview: true, adminReviewStatus: AdminReviewStatus.Rejected);

        var result = await sut.GetSuggestionsForStudentAsync(StudentId);

        result.ReviewItems.Should().BeEmpty();
    }

    // 23. Max visible scaffold suggestions cap respected.
    [Fact]
    public async Task GetSuggestions_PilotEnabled_RespectsMaxVisibleScaffoldCap()
    {
        var sut = BuildSut(new ReadinessPoolReplenishmentOptions
        {
            PracticeGymPilotEnabled = true,
            MaxStudentVisibleScaffoldSuggestions = 2
        });

        for (var i = 0; i < 4; i++)
        {
            SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true,
                requiresAdminReview: true, adminReviewStatus: AdminReviewStatus.Approved);
        }

        var result = await sut.GetSuggestionsForStudentAsync(StudentId);

        result.ReviewItems.Should().HaveCount(2);
    }

    // 24. Approved-but-unconsumed reserved scaffold item hidden by rollback (pilot disabled).
    [Fact]
    public async Task GetSuggestions_PilotDisabled_ApprovedReservedScaffoldItemHiddenFromContinue()
    {
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(2),
            routingReason: RoutingReason.Review, isLower: true,
            requiresAdminReview: true, adminReviewStatus: AdminReviewStatus.Approved);

        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);

        result.ContinueItems.Should().BeEmpty();
    }

    // 25. Approved scaffold item for another student is never visible to this student.
    [Fact]
    public async Task GetSuggestions_PilotEnabled_DoesNotLeakAnotherStudentsScaffoldItem()
    {
        var sut = BuildSut(new ReadinessPoolReplenishmentOptions { PracticeGymPilotEnabled = true });
        var otherStudentId = Guid.NewGuid();
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true,
            requiresAdminReview: true, adminReviewStatus: AdminReviewStatus.Approved, studentId: otherStudentId);

        var result = await sut.GetSuggestionsForStudentAsync(StudentId);

        result.ReviewItems.Should().BeEmpty();
    }

    // 26. Today lesson insertion is not exercised by this service — Practice Gym source only.
    // Structural proof lives in ReadinessPoolReplenishmentService (source-allowlist gate);
    // this asserts the suggestion service only ever queries the PracticeGym source.
    [Fact]
    public async Task GetSuggestions_OnlyQueriesPracticeGymSource()
    {
        var sut = BuildSut(new ReadinessPoolReplenishmentOptions { PracticeGymPilotEnabled = true });
        var todayLessonItem = new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.TodayLesson,
            targetCefrLevel: "B2",
            routingReason: RoutingReason.Review,
            isLowerLevelContent: true,
            requiresAdminReview: true);
        ForceStatus(todayLessonItem, ReadinessPoolStatus.ReviewOnly);
        ForceAdminReviewStatus(todayLessonItem, AdminReviewStatus.Approved);
        _db.StudentActivityReadinessItems.Add(todayLessonItem);
        _db.SaveChanges();

        var result = await sut.GetSuggestionsForStudentAsync(StudentId);

        result.ReviewItems.Should().BeEmpty();
        result.SuggestedItems.Should().BeEmpty();
    }

    // --- Phase 20H: duplicate suggestion dedupe ---

    // 27. Multiple Ready items sharing the same materialized activity id collapse to one Suggested card.
    [Fact]
    public async Task GetSuggestions_DuplicateReadyItemsSameActivity_CollapseToOneSuggestedCard()
    {
        var activityId = Guid.NewGuid();
        SeedItem(status: ReadinessPoolStatus.Ready, learningActivityId: activityId);
        SeedItem(status: ReadinessPoolStatus.Ready, learningActivityId: activityId);
        SeedItem(status: ReadinessPoolStatus.Ready, learningActivityId: activityId);

        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);

        result.SuggestedItems.Should().HaveCount(1);
    }

    // 28. An item reserved (Continue) for an activity is not also shown in Suggested/Review for
    // the same activity — Continue wins, no card appears in more than one bucket.
    [Fact]
    public async Task GetSuggestions_SameActivityInContinueAndReady_ContinueWinsAndNotDuplicatedInSuggested()
    {
        var activityId = Guid.NewGuid();
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(2), learningActivityId: activityId);
        SeedItem(status: ReadinessPoolStatus.Ready, learningActivityId: activityId);

        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);

        result.ContinueItems.Should().HaveCount(1);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 29. Caps still apply after dedupe — genuinely distinct activities beyond MaxSuggested (6) are still capped.
    [Fact]
    public async Task GetSuggestions_DistinctActivitiesBeyondCap_StillCapped()
    {
        for (var i = 0; i < 8; i++)
            SeedItem(status: ReadinessPoolStatus.Ready, learningActivityId: Guid.NewGuid());

        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);

        result.SuggestedItems.Should().HaveCount(6);
    }

    // 30. Live regression (2026-07-03): several distinct materialized activities for the same
    // objective/pattern — each queued separately before ReadinessPoolReplenishmentService's own
    // dedup fix caught up — must still collapse to one card. Different LearningActivityId per
    // item (unlike #27's same-activity case), so only the objective+pattern+type tier can catch it.
    [Fact]
    public async Task GetSuggestions_SameObjectiveAndPatternDifferentMaterializedActivities_CollapseToOneSuggestedCard()
    {
        for (var i = 0; i < 6; i++)
            SeedItem(status: ReadinessPoolStatus.Ready, learningActivityId: Guid.NewGuid(),
                curriculumObjectiveKey: "b2.speaking.structured_explanations",
                patternKey: "listening_multiple_choice_single");

        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);

        result.SuggestedItems.Should().HaveCount(1);
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

/// <summary>Stub replenishment service for unit tests — returns configurable health.</summary>
internal sealed class StubSettingsProvider : IEffectiveReadinessPoolSettingsProvider
{
    private readonly ReadinessPoolReplenishmentOptions _opts;

    public StubSettingsProvider(ReadinessPoolReplenishmentOptions opts) => _opts = opts;

    public Task<ReadinessPoolReplenishmentOptions> GetEffectiveAsync(CancellationToken ct = default) =>
        Task.FromResult(_opts);
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
