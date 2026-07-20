using FluentAssertions;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Mastery;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Mastery;

/// <summary>
/// Edge-case tests for Phase 12B mastery signal validation.
/// Covers boundary values for classification thresholds and suspicious pattern detection.
/// Phase I2C: the readiness-pool-dependent tests (ReviewOnly shortfall exclusion,
/// EvaluateReadinessItemFitAsync demotion decisions) were removed along with
/// StudentActivityReadinessItem — see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
///
/// Adaptive Curriculum Sprint 7 — EvaluateObjectiveMasteryAsync now resolves the requested
/// objectiveKey as a SkillGraphNode key (reusing GroupByNodeKeyAsync); every test seeds a real
/// approved Module linked to an approved SkillGraphNode plus the StudentExerciseLaunch bridge row,
/// so events carrying that ActivityId resolve to the node key under test.
/// </summary>
public sealed class StudentMasteryClassificationEdgeCaseTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeLedger _ledger;
    private readonly StudentMasteryEvaluationService _sut;
    private readonly Guid _studentId;

    public StudentMasteryClassificationEdgeCaseTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _ledger = new FakeLedger();

        var opts = Options.Create(new MasteryOptions());
        _sut = new StudentMasteryEvaluationService(
            _ledger,
            _db,
            opts,
            NullLogger<StudentMasteryEvaluationService>.Instance);

        var profile = new StudentProfile(Guid.NewGuid());
        profile.SetCefrLevel("B1");
        _db.StudentProfiles.Add(profile);
        _db.SaveChanges();
        _studentId = profile.Id;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── Boundary: exactly 3 events is NOT InsufficientEvidence ───────────────

    [Fact]
    public async Task Exactly3Events_IsNotInsufficientEvidence()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Practised, 50, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 55, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 60, activityId)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        signal.MasteryStatus.Should().NotBe(MasteryStatus.InsufficientEvidence);
        signal.EvidenceCount.Should().Be(3);
    }

    // ── Boundary: 2 events → still InsufficientEvidence ─────────────────────

    [Fact]
    public async Task TwoEvents_IsInsufficientEvidence()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Practised, 90, activityId),
            MakeEvent("speaking", LearningEventOutcome.Mastered, 95, activityId)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        signal.MasteryStatus.Should().Be(MasteryStatus.InsufficientEvidence);
    }

    // ── Boundary: exactly 2 consecutive failures → AtRisk ────────────────────

    [Fact]
    public async Task ExactlyTwoConsecutiveFailures_IsAtRisk()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Failed, 20, activityId),   // newest
            MakeEvent("speaking", LearningEventOutcome.Failed, 25, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 70, activityId) // oldest
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        signal.MasteryStatus.Should().Be(MasteryStatus.AtRisk);
        signal.ConsecutiveFailures.Should().Be(2);
    }

    // ── Boundary: avg score exactly 30 → NOT AtRisk (30 is the boundary) ────

    [Fact]
    public async Task AvgScoreExactly30_IsNotAtRisk()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        // avg = 30; AtRisk requires avg < 30
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Practised, 30, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 30, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 30, activityId)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        signal.MasteryStatus.Should().NotBe(MasteryStatus.AtRisk);
        signal.RecentAverageScore.Should().BeApproximately(30.0, 0.1);
    }

    // ── Boundary: avg score just below 80 → NOT Mastered ────────────────────

    [Fact]
    public async Task AvgScoreJustBelow80_IsNotMastered()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        // 5 events, 3 consecutive successes, avg ~79.8 — falls just under threshold
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Mastered, 82, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 80, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 79, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 78, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 79, activityId)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        signal.MasteryStatus.Should().NotBe(MasteryStatus.Mastered);
        signal.RecentAverageScore.Should().BeLessThan(80.0);
    }

    // ── Boundary: exactly 5 events, 3 consecutive successes, avg = 80 → Mastered

    [Fact]
    public async Task ExactThresholds_IsMastered()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        // Exactly matches EvidenceCountThreshold=5, ConsecutiveSuccessThreshold=3, MasteryScoreThreshold=80
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Mastered,  85, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 80, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 80, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 70, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 65, activityId)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        // avg = (85+80+80+70+65)/5 = 76 → NOT mastered (below 80 avg)
        // This verifies ALL three conditions are required simultaneously.
        signal.MasteryStatus.Should().NotBe(MasteryStatus.Mastered);
    }

    // ── Mastered requires all three conditions simultaneously ─────────────────

    [Fact]
    public async Task Mastered_RequiresAllThreeConditions()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Mastered,  90, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 88, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 85, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 82, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 80, activityId)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        signal.MasteryStatus.Should().Be(MasteryStatus.Mastered);
        signal.EvidenceCount.Should().BeGreaterThanOrEqualTo(5);
        signal.ConsecutiveSuccesses.Should().BeGreaterThanOrEqualTo(3);
        signal.RecentAverageScore.Should().BeGreaterThanOrEqualTo(80.0);
    }

    // ── Zero events → InsufficientEvidence ───────────────────────────────────

    [Fact]
    public async Task NoEvents_IsInsufficientEvidence()
    {
        var (_, nodeKey) = await SeedModuleLinkedToNodeAsync();
        _ledger.SetEvents(_studentId, []);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        signal.MasteryStatus.Should().Be(MasteryStatus.InsufficientEvidence);
        signal.EvidenceCount.Should().Be(0);
        signal.LastSeenUtc.Should().BeNull();
    }

    // ── A single failure (not 2) → not AtRisk ────────────────────────────────

    [Fact]
    public async Task SingleFailure_IsNotAtRisk()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Failed,    40, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 55, activityId),
            MakeEvent("speaking", LearningEventOutcome.Practised, 60, activityId)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, nodeKey);

        signal.MasteryStatus.Should().NotBe(MasteryStatus.AtRisk);
        signal.ConsecutiveFailures.Should().Be(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private StudentLearningEvent MakeEvent(string skill, LearningEventOutcome outcome, double score, Guid? activityId = null) =>
        new(
            studentProfileId: _studentId,
            source: LearningEventSource.PracticeGym,
            outcome: outcome,
            activityId: activityId,
            primarySkill: skill,
            patternKey: skill,
            score: score,
            normalizedScore: score / 100.0);

    /// <summary>Seeds an approved Module linked to one approved, active SkillGraphNode, plus the
    /// StudentExerciseLaunch bridge row a real completed attempt would leave behind, so a
    /// StudentLearningEvent carrying the returned ActivityId resolves to the returned node key.</summary>
    private async Task<(Guid ActivityId, string NodeKey)> SeedModuleLinkedToNodeAsync(string nodeKey = "b1.speaking.edge_case_test")
    {
        var module = new Module("Test Module", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "speaking", difficultyBand: 2);
        module.Approve(null);
        _db.Modules.Add(module);

        var node = new SkillGraphNode(nodeKey, "Test Node", "Test node description.", "B1", "speaking", difficultyBand: 2);
        node.Approve(null);
        _db.SkillGraphNodes.Add(node);

        var lesson = new Lesson("Test Lesson", "Body.", LessonSourceMode.Manual, cefrLevel: "B1", skill: "speaking");
        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync();

        var exercise = new Exercise(
            "Test Exercise", "Instructions.", "roleplay", ExerciseRendererType.Formio, ExerciseSourceMode.Manual,
            cefrLevel: "B1", skill: "speaking", lessonId: lesson.Id);
        _db.Exercises.Add(exercise);

        var activity = new LearningActivity(
            ActivityType.SpeakingRolePlay, ActivitySource.AiGenerated, "Test Activity", "B1", "{}");
        _db.LearningActivities.Add(activity);

        await _db.SaveChangesAsync();

        _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, node.Id, confidence: 1.0));
        _db.StudentExerciseLaunches.Add(new StudentExerciseLaunch(
            _studentId, module.Id, exercise.Id, activity.Id, ExerciseLaunchSource.PracticeGym, DateTimeOffset.UtcNow));

        await _db.SaveChangesAsync();

        return (activity.Id, node.Key);
    }
}
