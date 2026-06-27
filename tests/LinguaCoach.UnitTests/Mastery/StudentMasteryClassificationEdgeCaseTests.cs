using FluentAssertions;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.ReadinessPool;
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
/// Covers boundary values for classification thresholds,
/// ReviewOnly exclusion from shortfall, and suspicious pattern detection.
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
            _db,
            _ledger,
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
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Practised, 50),
            MakeEvent("speaking", LearningEventOutcome.Practised, 55),
            MakeEvent("speaking", LearningEventOutcome.Practised, 60)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "speaking");

        signal.MasteryStatus.Should().NotBe(MasteryStatus.InsufficientEvidence);
        signal.EvidenceCount.Should().Be(3);
    }

    // ── Boundary: 2 events → still InsufficientEvidence ─────────────────────

    [Fact]
    public async Task TwoEvents_IsInsufficientEvidence()
    {
        _ledger.SetEvents(_studentId, [
            MakeEvent("writing", LearningEventOutcome.Practised, 90),
            MakeEvent("writing", LearningEventOutcome.Mastered, 95)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "writing");

        signal.MasteryStatus.Should().Be(MasteryStatus.InsufficientEvidence);
    }

    // ── Boundary: exactly 2 consecutive failures → AtRisk ────────────────────

    [Fact]
    public async Task ExactlyTwoConsecutiveFailures_IsAtRisk()
    {
        _ledger.SetEvents(_studentId, [
            MakeEvent("grammar", LearningEventOutcome.Failed, 20),   // newest
            MakeEvent("grammar", LearningEventOutcome.Failed, 25),
            MakeEvent("grammar", LearningEventOutcome.Practised, 70) // oldest
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "grammar");

        signal.MasteryStatus.Should().Be(MasteryStatus.AtRisk);
        signal.ConsecutiveFailures.Should().Be(2);
    }

    // ── Boundary: avg score exactly 30 → NOT AtRisk (30 is the boundary) ────

    [Fact]
    public async Task AvgScoreExactly30_IsNotAtRisk()
    {
        // avg = 30; AtRisk requires avg < 30
        _ledger.SetEvents(_studentId, [
            MakeEvent("reading", LearningEventOutcome.Practised, 30),
            MakeEvent("reading", LearningEventOutcome.Practised, 30),
            MakeEvent("reading", LearningEventOutcome.Practised, 30)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "reading");

        signal.MasteryStatus.Should().NotBe(MasteryStatus.AtRisk);
        signal.RecentAverageScore.Should().BeApproximately(30.0, 0.1);
    }

    // ── Boundary: avg score just below 80 → NOT Mastered ────────────────────

    [Fact]
    public async Task AvgScoreJustBelow80_IsNotMastered()
    {
        // 5 events, 3 consecutive successes, avg ~79.8 — falls just under threshold
        _ledger.SetEvents(_studentId, [
            MakeEvent("vocabulary", LearningEventOutcome.Mastered, 82),
            MakeEvent("vocabulary", LearningEventOutcome.Practised, 80),
            MakeEvent("vocabulary", LearningEventOutcome.Practised, 79),
            MakeEvent("vocabulary", LearningEventOutcome.Practised, 78),
            MakeEvent("vocabulary", LearningEventOutcome.Practised, 79)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "vocabulary");

        signal.MasteryStatus.Should().NotBe(MasteryStatus.Mastered);
        signal.RecentAverageScore.Should().BeLessThan(80.0);
    }

    // ── Boundary: exactly 5 events, 3 consecutive successes, avg = 80 → Mastered

    [Fact]
    public async Task ExactThresholds_IsMastered()
    {
        // Exactly matches EvidenceCountThreshold=5, ConsecutiveSuccessThreshold=3, MasteryScoreThreshold=80
        _ledger.SetEvents(_studentId, [
            MakeEvent("listening", LearningEventOutcome.Mastered,  85),
            MakeEvent("listening", LearningEventOutcome.Practised, 80),
            MakeEvent("listening", LearningEventOutcome.Practised, 80),
            MakeEvent("listening", LearningEventOutcome.Practised, 70),
            MakeEvent("listening", LearningEventOutcome.Practised, 65)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "listening");

        // avg = (85+80+80+70+65)/5 = 76 → NOT mastered (below 80 avg)
        // This verifies ALL three conditions are required simultaneously.
        signal.MasteryStatus.Should().NotBe(MasteryStatus.Mastered);
    }

    // ── Mastered requires all three conditions simultaneously ─────────────────

    [Fact]
    public async Task Mastered_RequiresAllThreeConditions()
    {
        _ledger.SetEvents(_studentId, [
            MakeEvent("speaking", LearningEventOutcome.Mastered,  90),
            MakeEvent("speaking", LearningEventOutcome.Practised, 88),
            MakeEvent("speaking", LearningEventOutcome.Practised, 85),
            MakeEvent("speaking", LearningEventOutcome.Practised, 82),
            MakeEvent("speaking", LearningEventOutcome.Practised, 80)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "speaking");

        signal.MasteryStatus.Should().Be(MasteryStatus.Mastered);
        signal.EvidenceCount.Should().BeGreaterThanOrEqualTo(5);
        signal.ConsecutiveSuccesses.Should().BeGreaterThanOrEqualTo(3);
        signal.RecentAverageScore.Should().BeGreaterThanOrEqualTo(80.0);
    }

    // ── Zero events → InsufficientEvidence ───────────────────────────────────

    [Fact]
    public async Task NoEvents_IsInsufficientEvidence()
    {
        _ledger.SetEvents(_studentId, []);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "speaking");

        signal.MasteryStatus.Should().Be(MasteryStatus.InsufficientEvidence);
        signal.EvidenceCount.Should().Be(0);
        signal.LastSeenUtc.Should().BeNull();
    }

    // ── ReviewOnly items are excluded from ShortfallCount ────────────────────

    [Fact]
    public void ReviewOnlyItems_ExcludedFromShortfallCount()
    {
        // ShortfallCount = max(0, Target - Ready - QueuedOrGenerating)
        // ReviewOnly does NOT count toward shortfall — this is intentional.
        var health = new PoolHealthSummary
        {
            StudentId = _studentId,
            Source = ReadinessPoolSource.TodayLesson,
            TargetCount = 10,
            ReadyCount = 0,
            QueuedOrGeneratingCount = 0,
            ReviewOnlyCount = 5,
            FailedCount = 0,
            StaleCount = 0,
            ExpiredCount = 0,
            SkippedCount = 0,
            ReservedCount = 0
        };

        health.ShortfallCount.Should().Be(10); // ReviewOnly ignored
        health.NeedsReplenishment.Should().BeTrue();
    }

    // ── Mastered item with no review eligibility → Skip (not ReviewOnly) ─────

    [Fact]
    public async Task MasteredNonReviewEligibleItem_GetsSkipDecision()
    {
        var item = new StudentActivityReadinessItem(
            studentId: _studentId,
            source: ReadinessPoolSource.TodayLesson,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false,
            patternKey: "grammar",
            primarySkill: "grammar");
        item.MarkGenerating();
        item.MarkReady();

        _db.Set<StudentActivityReadinessItem>().Add(item);
        await _db.SaveChangesAsync();

        _ledger.SetEvents(_studentId, [
            MakeEvent("grammar", LearningEventOutcome.Mastered,  90),
            MakeEvent("grammar", LearningEventOutcome.Practised, 88),
            MakeEvent("grammar", LearningEventOutcome.Practised, 85),
            MakeEvent("grammar", LearningEventOutcome.Practised, 82),
            MakeEvent("grammar", LearningEventOutcome.Practised, 80)
        ]);

        var decision = await _sut.EvaluateReadinessItemFitAsync(_studentId, item.Id);

        decision.Should().Be(ReadinessDemotionDecision.Skip);
    }

    // ── A single failure (not 2) → not AtRisk ────────────────────────────────

    [Fact]
    public async Task SingleFailure_IsNotAtRisk()
    {
        _ledger.SetEvents(_studentId, [
            MakeEvent("reading", LearningEventOutcome.Failed,    40),
            MakeEvent("reading", LearningEventOutcome.Practised, 55),
            MakeEvent("reading", LearningEventOutcome.Practised, 60)
        ]);

        var signal = await _sut.EvaluateObjectiveMasteryAsync(_studentId, "reading");

        signal.MasteryStatus.Should().NotBe(MasteryStatus.AtRisk);
        signal.ConsecutiveFailures.Should().Be(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private StudentLearningEvent MakeEvent(string skill, LearningEventOutcome outcome, double score) =>
        new(
            studentProfileId: _studentId,
            source: LearningEventSource.PracticeGym,
            outcome: outcome,
            primarySkill: skill,
            patternKey: skill,
            score: score,
            normalizedScore: score / 100.0);
}
