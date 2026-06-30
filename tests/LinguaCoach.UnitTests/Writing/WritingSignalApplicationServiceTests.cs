using FluentAssertions;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Writing;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Writing;

/// <summary>
/// Unit tests for WritingEvaluationSignalApplicationService.
/// Verifies the 5-gate pipeline, idempotency, safety invariants, and config defaults.
/// Uses SQLite in-memory — no PostgreSQL required.
/// </summary>
public sealed class WritingSignalApplicationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly WritingEvaluationOptions _defaultOptions;

    public WritingSignalApplicationServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(dbOptions);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _defaultOptions = new WritingEvaluationOptions
        {
            ApplyMasterySignals = false,
            AllowReviewSignals = true,
            AllowPositiveSignals = false,
            MinimumConfidenceForMasterySignal = "High",
        };
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── Test 1: Config disabled prevents all signal application ──────────────

    [Fact]
    public async Task ApplyPendingSignals_ConfigDisabled_BlocksAllSignals()
    {
        var service = BuildService(_defaultOptions);  // ApplyMasterySignals = false
        var eval = SeedCompletedEvaluation(overallScore: 30, withFullScores: true);

        var result = await service.ApplyPendingSignalsAsync(10);

        result.Applied.Should().Be(0);
        result.BlockedByConfig.Should().Be(1);

        var applied = await _db.WritingEvaluationAppliedSignals.CountAsync();
        applied.Should().Be(0, "config disabled — no signal must be written");
    }

    // ── Test 2: Completed review candidate applies review signal when enabled ─

    [Fact]
    public async Task ApplyPendingSignals_ReviewCandidate_AppliesWhenEnabled()
    {
        var service = BuildService(new WritingEvaluationOptions
        {
            ApplyMasterySignals = true,
            AllowReviewSignals = true,
            AllowPositiveSignals = false,
            MinimumConfidenceForMasterySignal = "Medium",
        });

        // Low score + grammar score => CandidateReviewSignal at medium confidence
        SeedCompletedEvaluation(overallScore: 40, grammarScore: 40, vocabularyScore: 45, feedbackText: "Needs work");

        var result = await service.ApplyPendingSignalsAsync(10);

        result.Applied.Should().Be(1);

        var record = await _db.WritingEvaluationAppliedSignals.SingleAsync();
        record.SignalType.Should().Be("Review");
        record.SkillAffected.Should().Be("writing");
        record.AppliedRuleVersion.Should().Be("17C-v1");
    }

    // ── Test 3: Low confidence blocks signal ──────────────────────────────────

    [Fact]
    public async Task ApplyPendingSignals_LowConfidence_Blocked()
    {
        var service = BuildService(new WritingEvaluationOptions
        {
            ApplyMasterySignals = true,
            AllowReviewSignals = true,
            AllowPositiveSignals = false,
            MinimumConfidenceForMasterySignal = "High",
        });

        // OverallScore only — no dimension scores, no feedback => Low confidence
        SeedCompletedEvaluation(overallScore: 30, withFullScores: false, feedbackText: null);

        var result = await service.ApplyPendingSignalsAsync(10);

        result.Applied.Should().Be(0);
        var applied = await _db.WritingEvaluationAppliedSignals.CountAsync();
        applied.Should().Be(0, "low confidence must be blocked");
    }

    // ── Test 4: Positive candidate blocked when AllowPositiveSignals=false ────

    [Fact]
    public async Task ApplyPendingSignals_PositiveCandidate_BlockedByDefault()
    {
        var service = BuildService(new WritingEvaluationOptions
        {
            ApplyMasterySignals = true,
            AllowReviewSignals = true,
            AllowPositiveSignals = false,  // blocked
            MinimumConfidenceForMasterySignal = "High",
        });

        // High score, full data => CandidatePositiveSignal
        SeedCompletedEvaluation(overallScore: 85, grammarScore: 85, vocabularyScore: 85,
            coherenceScore: 85, taskCompletionScore: 85, feedbackText: "Excellent", correctedText: "Good");

        var result = await service.ApplyPendingSignalsAsync(10);

        result.Applied.Should().Be(0);
        result.BlockedBySignalType.Should().Be(1);
    }

    // ── Test 5: Positive signal applies when explicitly enabled + High conf ───

    [Fact]
    public async Task ApplyPendingSignals_PositiveCandidate_AppliesWhenExplicitlyEnabled()
    {
        var service = BuildService(new WritingEvaluationOptions
        {
            ApplyMasterySignals = true,
            AllowReviewSignals = true,
            AllowPositiveSignals = true,
            MinimumConfidenceForMasterySignal = "High",
        });

        SeedCompletedEvaluation(overallScore: 85, grammarScore: 85, vocabularyScore: 85,
            coherenceScore: 85, taskCompletionScore: 85, feedbackText: "Excellent", correctedText: "Good");

        var result = await service.ApplyPendingSignalsAsync(10);

        result.Applied.Should().Be(1);
        var record = await _db.WritingEvaluationAppliedSignals.SingleAsync();
        record.SignalType.Should().Be("Positive");
    }

    // ── Test 6: Applied signal is idempotent ──────────────────────────────────

    [Fact]
    public async Task ApplyPendingSignals_AlreadyApplied_DuplicateSkipped()
    {
        var service = BuildService(new WritingEvaluationOptions
        {
            ApplyMasterySignals = true,
            AllowReviewSignals = true,
            AllowPositiveSignals = false,
            MinimumConfidenceForMasterySignal = "Medium",
        });

        var eval = SeedCompletedEvaluation(overallScore: 40, grammarScore: 40, feedbackText: "Needs work");

        // Apply once
        await service.ApplyPendingSignalsAsync(10);
        var countAfterFirst = await _db.WritingEvaluationAppliedSignals.CountAsync();
        countAfterFirst.Should().Be(1);

        // Apply again — should skip
        var result = await service.ApplyPendingSignalsAsync(10);
        result.DuplicateSkipped.Should().Be(0, "already-applied evaluations are excluded from the query");
        var countAfterSecond = await _db.WritingEvaluationAppliedSignals.CountAsync();
        countAfterSecond.Should().Be(1, "idempotency: no duplicate applied signal");
    }

    // ── Test 7: Failed evaluation does not apply signal ───────────────────────

    [Fact]
    public async Task ApplyPendingSignals_FailedEvaluation_NotEligible()
    {
        var service = BuildService(new WritingEvaluationOptions { ApplyMasterySignals = true, AllowReviewSignals = true });

        var eval = WritingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        eval.MarkEvaluating("OpenAI", "gpt-4o-mini");
        eval.MarkFailed("provider timeout");
        _db.WritingEvaluations.Add(eval);
        await _db.SaveChangesAsync();

        var result = await service.ApplyPendingSignalsAsync(10);

        result.Processed.Should().Be(0, "failed evaluations are excluded from the Completed query");
        var applied = await _db.WritingEvaluationAppliedSignals.CountAsync();
        applied.Should().Be(0);
    }

    // ── Test 8: NotSupported evaluation does not apply signal ─────────────────

    [Fact]
    public async Task ApplyPendingSignals_NotSupportedEvaluation_NotEligible()
    {
        var service = BuildService(new WritingEvaluationOptions { ApplyMasterySignals = true, AllowReviewSignals = true });

        var eval = WritingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        eval.MarkEvaluating("NoOp", null);
        eval.MarkNotSupported();
        _db.WritingEvaluations.Add(eval);
        await _db.SaveChangesAsync();

        var result = await service.ApplyPendingSignalsAsync(10);

        result.Processed.Should().Be(0);
        var applied = await _db.WritingEvaluationAppliedSignals.CountAsync();
        applied.Should().Be(0);
    }

    // ── Test 9: Missing overall score blocks positive signal ──────────────────

    [Fact]
    public async Task ApplyPendingSignals_MissingOverallScore_Blocked()
    {
        var service = BuildService(new WritingEvaluationOptions { ApplyMasterySignals = true, AllowReviewSignals = true, AllowPositiveSignals = true });

        var eval = WritingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        eval.MarkEvaluating("OpenAI", "gpt-4o-mini");
        eval.MarkCompleted(
            overallScore: null,  // missing
            grammarScore: 80, vocabularyScore: 80, coherenceScore: 80, taskCompletionScore: 80,
            feedbackText: "Good", suggestedImprovement: null, correctedText: "Better");
        _db.WritingEvaluations.Add(eval);
        await _db.SaveChangesAsync();

        var result = await service.ApplyPendingSignalsAsync(10);

        result.Applied.Should().Be(0);
        var applied = await _db.WritingEvaluationAppliedSignals.CountAsync();
        applied.Should().Be(0, "missing overall score must block all signals");
    }

    // ── Test 10: CEFR is not changed ──────────────────────────────────────────

    [Fact]
    public void Options_AllowCefrUpdate_AlwaysFalse()
    {
        var opts = new WritingEvaluationOptions();
        opts.AllowCefrUpdate.Should().BeFalse("CEFR update from writing AI is permanently disabled");
    }

    // ── Test 11: Objective status is not completed ────────────────────────────

    [Fact]
    public void Options_AllowObjectiveCompletion_AlwaysFalse()
    {
        var opts = new WritingEvaluationOptions();
        opts.AllowObjectiveCompletion.Should().BeFalse("objective completion from writing AI is permanently disabled");
    }

    // ── Test 12: Safety summary confirms invariants ───────────────────────────

    [Fact]
    public async Task GetSignalSafetySummary_ConfirmsInvariants()
    {
        var service = BuildService(_defaultOptions);

        var safety = await service.GetSignalSafetySummaryAsync();

        safety.CefrUpdatesDisabled.Should().BeTrue();
        safety.ObjectiveCompletionsDisabled.Should().BeTrue();
        safety.LearningPlanAutoRegenDisabled.Should().BeTrue();
        safety.InvariantViolationsDetected.Should().BeFalse();
    }

    // ── Test 13: Summary counts match applied/blocked state ───────────────────

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectCounts()
    {
        var service = BuildService(new WritingEvaluationOptions
        {
            ApplyMasterySignals = true,
            AllowReviewSignals = true,
            AllowPositiveSignals = false,
            MinimumConfidenceForMasterySignal = "Medium",
        });

        SeedCompletedEvaluation(overallScore: 40, grammarScore: 40, feedbackText: "Needs work");
        await service.ApplyPendingSignalsAsync(10);

        var summary = await service.GetSummaryAsync();

        summary.TotalCompletedEvaluations.Should().Be(1);
        summary.AppliedSignals.Should().Be(1);
        summary.MasteryIntegrationEnabled.Should().BeTrue();
        summary.CefrUpdateAllowed.Should().BeFalse();
        summary.ObjectiveCompletionAllowed.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private WritingEvaluationSignalApplicationService BuildService(WritingEvaluationOptions options)
    {
        var opts = Options.Create(options);
        return new WritingEvaluationSignalApplicationService(_db, opts, NullLogger<WritingEvaluationSignalApplicationService>.Instance);
    }

    private WritingEvaluation SeedCompletedEvaluation(
        double? overallScore = 50,
        double? grammarScore = null,
        double? vocabularyScore = null,
        double? coherenceScore = null,
        double? taskCompletionScore = null,
        string? feedbackText = null,
        string? correctedText = null,
        bool withFullScores = false)
    {
        if (withFullScores)
        {
            grammarScore ??= overallScore;
            vocabularyScore ??= overallScore;
            coherenceScore ??= overallScore;
            taskCompletionScore ??= overallScore;
            feedbackText ??= "Some feedback";
            correctedText ??= "Some corrected text";
        }

        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);

        var eval = WritingEvaluation.CreatePending(Guid.NewGuid(), student.Id, Guid.NewGuid());
        eval.MarkEvaluating("OpenAI", "gpt-4o-mini");
        eval.MarkCompleted(overallScore, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
            feedbackText, null, correctedText);
        _db.WritingEvaluations.Add(eval);
        _db.SaveChanges();
        return eval;
    }
}
