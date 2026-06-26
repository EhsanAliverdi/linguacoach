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
            _db,
            _ledger,
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
    // 5. ReadinessItem converted to ReviewOnly when objective mastered
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ReadinessItem_ConvertedToReviewOnly_WhenObjectiveMastered()
    {
        // Item is lower-level content (review eligible)
        var item = MakeReadyItem(isLowerLevel: true, routingReason: RoutingReason.Review, patternKey: "speaking");
        _db.Set<StudentActivityReadinessItem>().Add(item);
        await _db.SaveChangesAsync();

        // Mastered signal
        _ledger.SetEvents(_studentId, MasteredEvents("speaking"));

        var decision = await _sut.EvaluateReadinessItemFitAsync(_studentId, item.Id);

        decision.Should().Be(ReadinessDemotionDecision.ConvertToReviewOnly);
    }

    // -------------------------------------------------------------------------
    // 6. ReadinessItem skipped when mastered and not useful for review
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ReadinessItem_Skipped_WhenMasteredAndNotUsefulForReview()
    {
        var item = MakeReadyItem(isLowerLevel: false, routingReason: RoutingReason.Normal, patternKey: "writing");
        _db.Set<StudentActivityReadinessItem>().Add(item);
        await _db.SaveChangesAsync();

        _ledger.SetEvents(_studentId, MasteredEvents("writing"));

        var decision = await _sut.EvaluateReadinessItemFitAsync(_studentId, item.Id);

        decision.Should().Be(ReadinessDemotionDecision.Skip);
    }

    // -------------------------------------------------------------------------
    // 7. ReadinessItem kept ready when skill is AtRisk
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ReadinessItem_KeepReady_WhenAtRisk()
    {
        var item = MakeReadyItem(isLowerLevel: false, routingReason: RoutingReason.Normal, patternKey: "listening");
        _db.Set<StudentActivityReadinessItem>().Add(item);
        await _db.SaveChangesAsync();

        _ledger.SetEvents(_studentId, [
            MakeEvent("listening", LearningEventOutcome.Failed, 20),
            MakeEvent("listening", LearningEventOutcome.Failed, 15),
            MakeEvent("listening", LearningEventOutcome.Practised, 40)
        ]);

        var decision = await _sut.EvaluateReadinessItemFitAsync(_studentId, item.Id);

        decision.Should().Be(ReadinessDemotionDecision.KeepReady);
    }

    // -------------------------------------------------------------------------
    // 8. No change for terminal state items
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ReadinessItem_NoChange_WhenTerminalState()
    {
        var item = MakeReadyItem(isLowerLevel: false, routingReason: RoutingReason.Normal, patternKey: "speaking");
        // MakeReadyItem leaves item in Ready state — advance to Consumed (terminal)
        item.Reserve();
        item.MarkConsumed();
        _db.Set<StudentActivityReadinessItem>().Add(item);
        await _db.SaveChangesAsync();

        _ledger.SetEvents(_studentId, MasteredEvents("speaking"));

        var decision = await _sut.EvaluateReadinessItemFitAsync(_studentId, item.Id);

        decision.Should().Be(ReadinessDemotionDecision.NoChange);
    }

    // -------------------------------------------------------------------------
    // 9. EvaluateAndDemote is idempotent (calling twice gives same count)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task EvaluateAndDemote_IsIdempotent()
    {
        // No items — both calls return 0
        var first = await _sut.EvaluateAndDemoteReadinessItemsAsync(_studentId);
        var second = await _sut.EvaluateAndDemoteReadinessItemsAsync(_studentId);

        first.Should().Be(0);
        second.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // 10. EvaluateAndDemote updates LastEvaluatedAtUtc
    // -------------------------------------------------------------------------
    [Fact]
    public async Task EvaluateAndDemote_UpdatesLastEvaluatedAtUtc()
    {
        var item = MakeReadyItem(isLowerLevel: false, routingReason: RoutingReason.Normal, patternKey: "speaking");
        _db.Set<StudentActivityReadinessItem>().Add(item);
        await _db.SaveChangesAsync();

        item.LastEvaluatedAtUtc.Should().BeNull();

        _ledger.SetEvents(_studentId, []);

        await _sut.EvaluateAndDemoteReadinessItemsAsync(_studentId);

        var updated = await _db.Set<StudentActivityReadinessItem>()
            .FirstAsync(i => i.Id == item.Id);
        updated.LastEvaluatedAtUtc.Should().NotBeNull();
        updated.LastEvaluatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // -------------------------------------------------------------------------
    // 11. EvaluateStudent returns mastered count
    // -------------------------------------------------------------------------
    [Fact]
    public async Task EvaluateStudent_ReturnsMasteredCount()
    {
        _ledger.SetEvents(_studentId, MasteredEvents("speaking"));

        var report = await _sut.EvaluateStudentAsync(_studentId, MasteryEvaluationReason.Manual);

        report.StudentId.Should().Be(_studentId);
        report.MasteredObjectiveKeys.Should().Contain("speaking");
        report.MasteredObjectiveKeys.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private StudentLearningEvent MakeEvent(string skill, LearningEventOutcome outcome, double score) =>
        new(
            studentProfileId: _studentId,
            source: LearningEventSource.PracticeGym,
            outcome: outcome,
            primarySkill: skill,
            patternKey: skill,
            score: score,
            normalizedScore: score / 100.0);

    private IReadOnlyList<StudentLearningEvent> MasteredEvents(string skill) =>
    [
        MakeEvent(skill, LearningEventOutcome.Mastered, 90),
        MakeEvent(skill, LearningEventOutcome.Practised, 85),
        MakeEvent(skill, LearningEventOutcome.Practised, 88),
        MakeEvent(skill, LearningEventOutcome.Practised, 82),
        MakeEvent(skill, LearningEventOutcome.Practised, 84)
    ];

    private StudentActivityReadinessItem MakeReadyItem(
        bool isLowerLevel,
        RoutingReason routingReason,
        string patternKey)
    {
        var item = new StudentActivityReadinessItem(
            studentId: _studentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B2",
            routingReason: routingReason,
            isLowerLevelContent: isLowerLevel,
            patternKey: patternKey,
            primarySkill: patternKey);
        item.MarkGenerating();
        item.MarkReady();
        return item;
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
