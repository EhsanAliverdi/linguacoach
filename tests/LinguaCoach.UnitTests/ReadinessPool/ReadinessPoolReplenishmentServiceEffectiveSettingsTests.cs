using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ReadinessPool;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinguaCoach.UnitTests.ReadinessPool;

/// <summary>
/// Unit tests proving effective runtime settings (Phase 20C) actually drive
/// ReadinessPoolReplenishmentService behavior — not just appsettings. Uses a real
/// StudentActivityReadinessPoolService against SQLite in-memory plus lightweight fakes for
/// mastery/ledger/routing/goal-context so the review/scaffold branch can be triggered
/// deterministically without seeding curriculum data or AI calls.
/// </summary>
public sealed class ReadinessPoolReplenishmentServiceEffectiveSettingsTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _profileId;

    public ReadinessPoolReplenishmentServiceEffectiveSettingsTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var student = new StudentProfile(_studentId);
        student.SetLifecycleStage(StudentLifecycleStage.CourseReady);
        // Force onboarding-complete for test purposes without running the full onboarding
        // state machine — same reflection-based test-setup pattern used elsewhere in this suite
        // (see LearningGoalContextResolverTests).
        typeof(StudentProfile).GetProperty(nameof(StudentProfile.OnboardingStatus))!
            .SetValue(student, OnboardingStatus.Complete);
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        // StudentProfile(Guid userId) stores the ctor arg as UserId, not Id — StudentActivityReadinessItem.StudentId
        // (and every readiness-pool query) is keyed on profile.Id, which is auto-generated.
        _profileId = student.Id;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private ReadinessPoolReplenishmentService BuildSut(ReadinessPoolReplenishmentOptions opts, bool allowReviewOrScaffoldRouting = true) =>
        new(
            _db,
            new StudentActivityReadinessPoolService(_db, NullLogger<StudentActivityReadinessPoolService>.Instance),
            new FakeLedger(),
            new FakeMastery(),
            new FakeGoalResolver(),
            new FakeRoutingService(allowReviewOrScaffoldRouting),
            new FixedSettingsProvider(opts),
            NullLogger<ReadinessPoolReplenishmentService>.Instance);

    private IQueryable<StudentActivityReadinessItem> ScaffoldItems() =>
        _db.StudentActivityReadinessItems.Where(i => i.RoutingReason != RoutingReason.Normal);

    [Fact]
    public async Task Default_EnableReviewScaffoldGenerationFalse_NoScaffoldItemsCreated()
    {
        var sut = BuildSut(new ReadinessPoolReplenishmentOptions()); // all defaults: Enable=false

        await sut.RunAsync();

        Assert.Empty(ScaffoldItems());
    }

    [Fact]
    public async Task Override_EnableTrue_DryRunOnlyTrue_ComputesButDoesNotPersist()
    {
        var opts = new ReadinessPoolReplenishmentOptions
        {
            EnableReviewScaffoldGeneration = true,
            DryRunOnly = true, // explicit, matches the safe default
            ScaffoldAllowedSources = ["TodayLesson", "PracticeGym"],
        };
        var sut = BuildSut(opts);

        await sut.RunAsync();

        Assert.Empty(ScaffoldItems());
    }

    [Fact]
    public async Task Override_EnableTrue_DryRunOnlyFalse_PersistsScaffoldItem()
    {
        var opts = new ReadinessPoolReplenishmentOptions
        {
            EnableReviewScaffoldGeneration = true,
            DryRunOnly = false,
            ScaffoldAllowedSources = ["TodayLesson", "PracticeGym"],
        };
        var sut = BuildSut(opts);

        await sut.RunAsync();

        Assert.NotEmpty(ScaffoldItems());
    }

    [Fact]
    public async Task Override_RequireAdminReviewTrue_PersistedScaffoldItemRequiresReview()
    {
        var opts = new ReadinessPoolReplenishmentOptions
        {
            EnableReviewScaffoldGeneration = true,
            DryRunOnly = false,
            RequireAdminReview = true,
            ScaffoldAllowedSources = ["TodayLesson", "PracticeGym"],
        };
        var sut = BuildSut(opts);

        await sut.RunAsync();

        var item = Assert.Single(ScaffoldItems());
        Assert.True(item.RequiresAdminReview);
    }

    [Fact]
    public async Task Override_MaxScaffoldItemsPerStudentPerDayZero_BlocksScaffoldGenerationFromFirstAttempt()
    {
        // A cap of 0 means "0 existing scaffold items today already satisfies the >= cap
        // check," so no scaffold item should ever be created — proving the override value
        // (not just the appsettings default of 3) is what the service consults.
        var opts = new ReadinessPoolReplenishmentOptions
        {
            EnableReviewScaffoldGeneration = true,
            DryRunOnly = false,
            MaxScaffoldItemsPerStudentPerDay = 0,
            ScaffoldAllowedSources = ["PracticeGym"],
        };
        var sut = BuildSut(opts);

        await sut.RunAsync();

        Assert.Empty(ScaffoldItems());
    }

    // --- Phase 20H: TODO-20G-1 duplicate-suggestion regression ---

    // A materialized item (real PatternKey assigned post-generation, as PracticeGymGenerationJob
    // does) for an objective/level must stop further replenishment runs from re-queuing more
    // items for that same objective/level — even though the new candidate's dedup key is always
    // computed with a null PatternKey at queue time (pattern isn't chosen until materialization).
    [Fact]
    public async Task MaterializedItemWithRealPatternKey_PreventsReQueueingSameObjectiveAndLevel()
    {
        var existing = new StudentActivityReadinessItem(
            studentId: _profileId, source: ReadinessPoolSource.PracticeGym, targetCefrLevel: "B1",
            routingReason: RoutingReason.Normal, isLowerLevelContent: false,
            curriculumObjectiveKey: "test-objective", patternKey: "listening_multiple_choice_single");
        _db.StudentActivityReadinessItems.Add(existing);
        existing.MarkGenerating();
        existing.MarkReady();
        await _db.SaveChangesAsync();

        var opts = new ReadinessPoolReplenishmentOptions(); // defaults: PracticeGymPoolTargetCount=10
        var sut = BuildSut(opts);

        await sut.RunAsync();

        var practiceGymItems = _db.StudentActivityReadinessItems
            .Where(i => i.Source == ReadinessPoolSource.PracticeGym
                     && i.CurriculumObjectiveKey == "test-objective"
                     && i.TargetCefrLevel == "B1")
            .ToList();

        // FakeRoutingService always recommends the same objective/level regardless of primary
        // skill, so every FillShortfallAsync slot in this run targets the same duplicate key —
        // only the one pre-seeded item should remain.
        Assert.Single(practiceGymItems);
    }

    // --- fakes ---

    private sealed class FixedSettingsProvider(ReadinessPoolReplenishmentOptions opts) : IEffectiveReadinessPoolSettingsProvider
    {
        public Task<ReadinessPoolReplenishmentOptions> GetEffectiveAsync(CancellationToken ct = default) =>
            Task.FromResult(opts);
    }

    private sealed class FakeLedger : IStudentLearningLedger
    {
        public Task RecordAsync(StudentLearningEvent learningEvent, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StudentLearningEvent>> GetRecentAsync(Guid studentProfileId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudentLearningEvent>>([]);

        public Task<IReadOnlyList<StudentLearningEvent>> GetRecentByPatternKeysAsync(Guid studentProfileId, IEnumerable<string> patternKeys, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudentLearningEvent>>([]);

        public Task<IReadOnlyList<string>> GetRecentPatternKeysAsync(Guid studentProfileId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        // Always report one weak event so the review/scaffold branch is reachable in every test.
        public Task<IReadOnlyList<StudentLearningEvent>> GetWeakEventsAsync(Guid studentProfileId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudentLearningEvent>>([
                new StudentLearningEvent(studentProfileId, LearningEventSource.PracticeGym, LearningEventOutcome.NeedsReview),
            ]);
    }

    private sealed class FakeMastery : IStudentMasteryEvaluationService
    {
        public Task<StudentMasteryReport> EvaluateStudentAsync(Guid studentId, MasteryEvaluationReason reason, CancellationToken ct = default) =>
            Task.FromResult(new StudentMasteryReport
            {
                StudentId = studentId,
                EvaluatedAtUtc = DateTime.UtcNow,
                Reason = reason,
                MasteredObjectiveKeys = [],
                CompletedObjectiveKeys = [],
                WeakObjectiveKeys = ["weak-objective"], // → ReviewNeedConfidence.Medium, meets default "Medium" threshold
                AtRiskObjectiveKeys = [],
                DemotedCount = 0,
                SkippedCount = 0,
                MarkedReviewOnlyCount = 0,
            });

        public Task<ObjectiveMasterySignal> EvaluateObjectiveMasteryAsync(Guid studentId, string objectiveKey, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by ReadinessPoolReplenishmentService.");

        public Task<ReadinessDemotionDecision> EvaluateReadinessItemFitAsync(Guid studentId, Guid readinessItemId, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by ReadinessPoolReplenishmentService.");

        public Task<int> EvaluateAndDemoteReadinessItemsAsync(Guid studentId, CancellationToken ct = default) =>
            Task.FromResult(0);
    }

    private sealed class FakeGoalResolver : ILearningGoalContextResolver
    {
        public ResolvedLearningGoalContext Resolve(StudentProfile profile, LearningGoalResolutionContext? context = null) => new()
        {
            ContextSummary = "general_english",
            Source = "Fallback",
        };
    }

    private sealed class FakeRoutingService(bool respectAllowReviewOrScaffold) : ICurriculumRoutingService
    {
        public Task<CurriculumRoutingRecommendation> RecommendAsync(CurriculumRoutingRequest request, CancellationToken ct = default) =>
            Task.FromResult(new CurriculumRoutingRecommendation
            {
                TargetCefrLevel = "B1",
                PrimarySkill = request.PrimarySkill ?? "writing",
                CurriculumObjectiveKey = "test-objective",
                CurriculumObjectiveTitle = "Test objective",
                RoutingReason = respectAllowReviewOrScaffold && request.AllowReviewOrScaffold
                    ? RoutingReason.Review
                    : RoutingReason.Normal,
                IsLowerLevelContent = respectAllowReviewOrScaffold && request.AllowReviewOrScaffold,
                Source = request.Source,
            });

        public string NormalizeCefrLevel(string? rawLevel) => rawLevel ?? "A1";
    }
}
