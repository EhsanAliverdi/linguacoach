using FluentAssertions;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Mastery;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Mastery;

/// <summary>
/// Unit tests for StudentMasteryEvaluationService (Phase 10Z).
/// Uses SQLite in-memory for DB and a hand-rolled FakeLedger for learning events.
/// No AI calls; all rules are deterministic.
/// Phase I2C: the readiness-pool demotion tests (EvaluateReadinessItemFitAsync/
/// EvaluateAndDemoteReadinessItemsAsync) were removed along with StudentActivityReadinessItem —
/// see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed class StudentMasteryEvaluationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeLedger _ledger;
    private readonly StudentMasteryEvaluationService _sut;
    private readonly Guid _studentId;

    public StudentMasteryEvaluationServiceTests()
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

        // Seed a student profile
        var profile = new StudentProfile(Guid.NewGuid());
        profile.SetCefrLevel("B2");
        _db.StudentProfiles.Add(profile);
        _db.SaveChanges();
        _studentId = profile.Id;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // -------------------------------------------------------------------------
    // 1. InsufficientEvidence when fewer than 3 events
    // -------------------------------------------------------------------------
    [Fact]
    public async Task MasteryStatus_InsufficientEvidence_WhenFewerThan3Events()
    {
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Practised, 80)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "speaking");

        signal.MasteryStatus.Should().Be(MasteryStatus.InsufficientEvidence);
        signal.EvidenceCount.Should().Be(1);
    }

    // -------------------------------------------------------------------------
    // 2. Mastered when >= 5 events, 3 consecutive successes, avg >= 80
    // -------------------------------------------------------------------------
    [Fact]
    public async Task MasteryStatus_Mastered_When5EventsConsecutiveSuccessHighScore()
    {
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Mastered, 90),
            MakeEvent("speaking", LearningEventOutcome.Practised, 85),
            MakeEvent("speaking", LearningEventOutcome.Practised, 88),
            MakeEvent("speaking", LearningEventOutcome.Practised, 82),
            MakeEvent("speaking", LearningEventOutcome.Practised, 84)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "speaking");

        signal.MasteryStatus.Should().Be(MasteryStatus.Mastered);
        signal.ConsecutiveSuccesses.Should().BeGreaterOrEqualTo(3);
        signal.RecentAverageScore.Should().BeGreaterOrEqualTo(80);
    }

    // -------------------------------------------------------------------------
    // 3. AtRisk when 2+ consecutive failures (most-recent first)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task MasteryStatus_AtRisk_WhenConsecutiveFailures()
    {
        // Events are ordered newest-first by the ledger. Two most-recent are failures.
        _ledger.SetEvents(_studentId, [
            MakeEvent("grammar", LearningEventOutcome.Failed, 20),  // newest
            MakeEvent("grammar", LearningEventOutcome.Failed, 15),
            MakeEvent("grammar", LearningEventOutcome.Practised, 70) // oldest
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "grammar");

        signal.MasteryStatus.Should().Be(MasteryStatus.AtRisk);
        signal.ConsecutiveFailures.Should().BeGreaterOrEqualTo(2);
    }

    // -------------------------------------------------------------------------
    // 4. NeedsReview when mixed but last event success, avg 50–79
    // -------------------------------------------------------------------------
    [Fact]
    public async Task MasteryStatus_NeedsReview_WhenMixedButHighAvg()
    {
        _ledger.SetEvents(_studentId, [
            MakeEvent("vocabulary", LearningEventOutcome.Practised, 75),
            MakeEvent("vocabulary", LearningEventOutcome.NeedsReview, 55),
            MakeEvent("vocabulary", LearningEventOutcome.Practised, 70)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "vocabulary");

        signal.MasteryStatus.Should().BeOneOf(MasteryStatus.NeedsReview, MasteryStatus.NeedsPractice);
        signal.EvidenceCount.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // 11. Adaptive Curriculum Sprint 4 — EvaluateStudentAsync groups by resolved SkillGraphNode
    //     key, not by skill/objective string. An event only counts once it resolves through
    //     ActivityId -> StudentExerciseLaunch -> Module -> ModuleSkillGraphNodeLink -> SkillGraphNode.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task EvaluateStudent_ReturnsMasteredCount_ForNodeLinkedEvents()
    {
        var (activityId, nodeKey) = await SeedModuleLinkedToNodeAsync();
        _ledger.SetEvents(_studentId, MasteredEvents("speaking", activityId));

        var report = await _sut.EvaluateStudentAsync(_studentId, MasteryEvaluationReason.Manual);

        report.StudentId.Should().Be(_studentId);
        report.MasteredObjectiveKeys.Should().Contain(nodeKey);
        report.MasteredObjectiveKeys.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    // -------------------------------------------------------------------------
    // 12. Adaptive Curriculum Sprint 4 — events with no resolvable node (legacy content, no
    //     StudentExerciseLaunch row) contribute to no bucket: EvaluateStudentAsync's node-based
    //     grouping is a hard cutover, not a fallback onto the old skill/objective-key grouping.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task EvaluateStudent_LegacyEventsWithNoActivityId_ProduceNoMasteredKeys()
    {
        _ledger.SetEvents(_studentId, MasteredEvents("speaking"));

        var report = await _sut.EvaluateStudentAsync(_studentId, MasteryEvaluationReason.Manual);

        report.MasteredObjectiveKeys.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // 13. Adaptive Curriculum Sprint 4 — one Module linked to multiple nodes fans a single
    //     event set out into every linked node's evidence bucket (mirrors Sprint 3's goal-tag
    //     fan-out for implicit engagement drift).
    // -------------------------------------------------------------------------
    [Fact]
    public async Task EvaluateStudent_FansOutToEveryLinkedNode()
    {
        var (activityId, firstNodeKey, secondNodeKey) = await SeedModuleLinkedToTwoNodesAsync();
        _ledger.SetEvents(_studentId, MasteredEvents("speaking", activityId));

        var report = await _sut.EvaluateStudentAsync(_studentId, MasteryEvaluationReason.Manual);

        report.MasteredObjectiveKeys.Should().Contain(firstNodeKey);
        report.MasteredObjectiveKeys.Should().Contain(secondNodeKey);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private StudentLearningEvent MakeEventWithObjective(
        string patternKey, string curriculumObjectiveKey, LearningEventOutcome outcome, double score) =>
        new(
            studentProfileId: _studentId,
            source: LearningEventSource.PracticeGym,
            outcome: outcome,
            patternKey: patternKey,
            curriculumObjectiveKey: curriculumObjectiveKey,
            score: score,
            normalizedScore: score / 100.0);

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

    private IReadOnlyList<StudentLearningEvent> MasteredEvents(string skill, Guid? activityId = null) =>
    [
        MakeEvent(skill, LearningEventOutcome.Mastered, 90, activityId),
        MakeEvent(skill, LearningEventOutcome.Practised, 85, activityId),
        MakeEvent(skill, LearningEventOutcome.Practised, 88, activityId),
        MakeEvent(skill, LearningEventOutcome.Practised, 82, activityId),
        MakeEvent(skill, LearningEventOutcome.Practised, 84, activityId)
    ];

    /// <summary>Seeds an approved Module linked to one approved, active SkillGraphNode, plus the
    /// StudentExerciseLaunch bridge row a real completed attempt would leave behind, so a
    /// StudentLearningEvent carrying the returned ActivityId resolves to the returned node key.</summary>
    private async Task<(Guid ActivityId, string NodeKey)> SeedModuleLinkedToNodeAsync(string nodeKey = "b2.speaking.fluency_test")
    {
        var module = new Module("Test Module", ModuleSourceMode.Manual, cefrLevel: "B2", skill: "speaking", difficultyBand: 2);
        module.Approve(null);
        _db.Modules.Add(module);

        var node = new SkillGraphNode(nodeKey, "Test Node", "Test node description.", "B2", "speaking", difficultyBand: 2);
        node.Approve(null);
        _db.SkillGraphNodes.Add(node);

        var (exerciseId, activityId) = await SeedExerciseAndActivityAsync();

        await _db.SaveChangesAsync();

        _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, node.Id, confidence: 1.0));
        _db.StudentExerciseLaunches.Add(new StudentExerciseLaunch(
            _studentId, module.Id, exerciseId, activityId, ExerciseLaunchSource.PracticeGym, DateTimeOffset.UtcNow));

        await _db.SaveChangesAsync();

        return (activityId, node.Key);
    }

    /// <summary>Seeds the minimal real Lesson/Exercise/LearningActivity chain StudentExerciseLaunch's
    /// restrict-delete FKs require, and returns their ids for use in a launch row.</summary>
    private async Task<(Guid ExerciseId, Guid ActivityId)> SeedExerciseAndActivityAsync()
    {
        var lesson = new Lesson("Test Lesson", "Body.", LessonSourceMode.Manual, cefrLevel: "B2", skill: "speaking");
        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync();

        var exercise = new Exercise(
            "Test Exercise", "Instructions.", "roleplay", ExerciseRendererType.Formio, ExerciseSourceMode.Manual,
            cefrLevel: "B2", skill: "speaking", lessonId: lesson.Id);
        _db.Exercises.Add(exercise);

        var activity = new LearningActivity(
            ActivityType.SpeakingRolePlay, ActivitySource.AiGenerated, "Test Activity", "B2", "{}");
        _db.LearningActivities.Add(activity);

        await _db.SaveChangesAsync();

        return (exercise.Id, activity.Id);
    }

    /// <summary>Same as <see cref="SeedModuleLinkedToNodeAsync"/> but one Module linked to two
    /// nodes, to verify one event set fans out into both buckets.</summary>
    private async Task<(Guid ActivityId, string FirstNodeKey, string SecondNodeKey)> SeedModuleLinkedToTwoNodesAsync()
    {
        var module = new Module("Fan-Out Test Module", ModuleSourceMode.Manual, cefrLevel: "B2", skill: "speaking", difficultyBand: 2);
        module.Approve(null);
        _db.Modules.Add(module);

        var firstNode = new SkillGraphNode("b2.speaking.fanout_a", "Fan-Out Node A", "Test node description.", "B2", "speaking", difficultyBand: 2);
        firstNode.Approve(null);
        var secondNode = new SkillGraphNode("b2.speaking.fanout_b", "Fan-Out Node B", "Test node description.", "B2", "speaking", difficultyBand: 3);
        secondNode.Approve(null);
        _db.SkillGraphNodes.AddRange(firstNode, secondNode);

        await _db.SaveChangesAsync();

        var (exerciseId, activityId) = await SeedExerciseAndActivityAsync();

        _db.ModuleSkillGraphNodeLinks.AddRange(
            new ModuleSkillGraphNodeLink(module.Id, firstNode.Id, confidence: 1.0),
            new ModuleSkillGraphNodeLink(module.Id, secondNode.Id, confidence: 1.0));
        _db.StudentExerciseLaunches.Add(new StudentExerciseLaunch(
            _studentId, module.Id, exerciseId, activityId, ExerciseLaunchSource.PracticeGym, DateTimeOffset.UtcNow));

        await _db.SaveChangesAsync();

        return (activityId, firstNode.Key, secondNode.Key);
    }

}

// ---------------------------------------------------------------------------
// Test double — in-memory ledger
// ---------------------------------------------------------------------------
internal sealed class FakeLedger : IStudentLearningLedger
{
    private readonly Dictionary<Guid, IReadOnlyList<StudentLearningEvent>> _store = new();

    public void SetEvents(Guid studentId, IReadOnlyList<StudentLearningEvent> events)
        => _store[studentId] = events;

    public Task RecordAsync(StudentLearningEvent learningEvent, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(learningEvent.StudentProfileId, out var existing))
            existing = [];
        _store[learningEvent.StudentProfileId] = [learningEvent, ..existing];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StudentLearningEvent>> GetRecentAsync(
        Guid studentProfileId, int limit = 50, CancellationToken ct = default)
    {
        var events = _store.TryGetValue(studentProfileId, out var list)
            ? list.Take(limit).ToList()
            : [];
        return Task.FromResult<IReadOnlyList<StudentLearningEvent>>(events);
    }

    public Task<IReadOnlyList<StudentLearningEvent>> GetRecentByPatternKeysAsync(
        Guid studentProfileId, IEnumerable<string> patternKeys, int limit = 20, CancellationToken ct = default)
    {
        var keys = patternKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var events = _store.TryGetValue(studentProfileId, out var list)
            ? list.Where(e => e.PatternKey is not null && keys.Contains(e.PatternKey)).Take(limit).ToList()
            : [];
        return Task.FromResult<IReadOnlyList<StudentLearningEvent>>(events);
    }

    public Task<IReadOnlyList<string>> GetRecentPatternKeysAsync(
        Guid studentProfileId, int limit = 20, CancellationToken ct = default)
    {
        var keys = _store.TryGetValue(studentProfileId, out var list)
            ? list.Where(e => e.PatternKey is not null)
                  .Select(e => e.PatternKey!)
                  .Distinct()
                  .Take(limit)
                  .ToList()
            : [];
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    public Task<IReadOnlyList<StudentLearningEvent>> GetWeakEventsAsync(
        Guid studentProfileId, int limit = 20, CancellationToken ct = default)
    {
        var weakOutcomes = new HashSet<LearningEventOutcome>
        {
            LearningEventOutcome.NeedsReview,
            LearningEventOutcome.Failed
        };
        var events = _store.TryGetValue(studentProfileId, out var list)
            ? list.Where(e => weakOutcomes.Contains(e.Outcome)).Take(limit).ToList()
            : [];
        return Task.FromResult<IReadOnlyList<StudentLearningEvent>>(events);
    }
}
