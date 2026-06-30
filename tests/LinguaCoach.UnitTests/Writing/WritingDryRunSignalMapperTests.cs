using FluentAssertions;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Writing;

/// <summary>
/// Unit tests for WritingDryRunSignalMapper.
/// Validates signal classification, confidence bands, and safety invariants.
/// Phase 17B — dry-run only. Never applied to mastery, CEFR, or Learning Plan.
/// </summary>
public sealed class WritingDryRunSignalMapperTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static WritingEvaluation MakeCompleted(
        double? overallScore = 80.0,
        double? grammarScore = 80.0,
        double? vocabularyScore = 80.0,
        double? coherenceScore = 80.0,
        double? taskCompletionScore = 80.0,
        string? feedbackText = "Good work.",
        string? correctedText = "Corrected version.")
    {
        var eval = WritingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        eval.MarkEvaluating("TestProvider", "test-model");
        eval.MarkCompleted(overallScore, grammarScore, vocabularyScore, coherenceScore, taskCompletionScore,
            feedbackText, null, correctedText);
        return eval;
    }

    private static WritingEvaluation MakePending() =>
        WritingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static WritingEvaluation MakeFailed()
    {
        var eval = WritingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        eval.MarkEvaluating("TestProvider", null);
        eval.MarkFailed("API timeout");
        return eval;
    }

    private static WritingEvaluation MakeNotSupported()
    {
        var eval = WritingEvaluation.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        eval.MarkNotSupported();
        return eval;
    }

    // ── Test 1: Empty evaluations list returns safe state ─────────────────────

    [Fact]
    public void Map_EmptyList_NoExceptions()
    {
        // Calling mapper on an empty list returns empty — safe.
        var evals = new List<WritingEvaluation>();
        var signals = evals.Select(WritingDryRunSignalMapper.Map).ToList();
        signals.Should().BeEmpty();
    }

    // ── Test 2: Status counts correct ─────────────────────────────────────────

    [Fact]
    public void Map_Mixed_ReturnsCorrectOutcomeDistribution()
    {
        var evals = new List<WritingEvaluation>
        {
            MakeCompleted(),
            MakeFailed(),
            MakeNotSupported(),
            MakePending(),
        };

        var signals = evals.Select(WritingDryRunSignalMapper.Map).ToList();

        signals.Should().HaveCount(4);
        signals.Count(s => s.IsBlocked).Should().Be(3); // Failed, NotSupported, Pending
        signals.Count(s => s.IsCandidate).Should().Be(1); // Completed with good scores
    }

    // ── Test 3: Completion/failure rates correct (via MapFromFields) ──────────

    [Fact]
    public void MapFromFields_Completed_ReturnsNonBlockedOutcome()
    {
        var signal = WritingDryRunSignalMapper.MapFromFields(
            evalId: Guid.NewGuid(),
            attemptId: Guid.NewGuid(),
            studentId: Guid.NewGuid(),
            activityId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            providerName: "TestProvider",
            modelName: "test-model",
            status: WritingEvaluationStatus.Completed,
            overallScore: 80.0,
            grammarScore: 80.0,
            vocabularyScore: null,
            coherenceScore: null,
            taskCompletionScore: null,
            feedbackText: "Good work.",
            correctedText: null);

        signal.IsBlocked.Should().BeFalse();
        signal.Outcome.Should().NotBe(WritingDryRunSignalOutcome.BlockedFailedEvaluation);
    }

    // ── Test 4: Null score rates correct ──────────────────────────────────────

    [Fact]
    public void Map_CompletedWithAllNullScores_BlockedMissingScore()
    {
        var eval = MakeCompleted(
            overallScore: null,
            grammarScore: null,
            vocabularyScore: null,
            coherenceScore: null,
            taskCompletionScore: null,
            feedbackText: "ok",
            correctedText: null);

        var signal = WritingDryRunSignalMapper.Map(eval);

        signal.Outcome.Should().Be(WritingDryRunSignalOutcome.BlockedMissingScore);
        signal.IsBlocked.Should().BeTrue();
    }

    // ── Test 5: Corrected text availability rate ───────────────────────────────

    [Fact]
    public void Map_HighConfidenceRequiresCorrectedText()
    {
        // Without corrected text, confidence drops below High.
        var withoutCorrected = MakeCompleted(correctedText: null);
        var withCorrected = MakeCompleted(correctedText: "Corrected.");

        var s1 = WritingDryRunSignalMapper.Map(withoutCorrected);
        var s2 = WritingDryRunSignalMapper.Map(withCorrected);

        // correctedText=null means not High confidence even if everything else is good.
        s1.ConfidenceBand.Should().NotBe(WritingDryRunConfidenceBand.High);
        s2.ConfidenceBand.Should().Be(WritingDryRunConfidenceBand.High);
    }

    // ── Test 6: High confidence + strong scores → CandidatePositiveSignal ─────

    [Fact]
    public void Map_HighConfidenceStrongScores_CandidatePositiveSignal()
    {
        var eval = MakeCompleted(
            overallScore: 80.0,
            grammarScore: 80.0,
            vocabularyScore: 82.0,
            coherenceScore: 78.0,
            taskCompletionScore: 76.0,
            feedbackText: "Well done.",
            correctedText: "Corrected.");

        var signal = WritingDryRunSignalMapper.Map(eval);

        signal.Outcome.Should().Be(WritingDryRunSignalOutcome.CandidatePositiveSignal);
        signal.SuggestedMasteryDelta.Should().BeGreaterThan(0.0);
        signal.SuggestedMasteryDelta.Should().BeInRange(0.05, 0.25);
        signal.SuggestedReviewNeed.Should().BeFalse();
        signal.AcceptedForFutureSignal.Should().BeTrue();
        signal.ConfidenceBand.Should().Be(WritingDryRunConfidenceBand.High);
    }

    // ── Test 7: Weak grammar/coherence → CandidateReviewSignal ───────────────

    [Fact]
    public void Map_WeakGrammarScore_CandidateReviewSignal()
    {
        // Medium confidence (no corrected text) + weak grammar
        var eval = MakeCompleted(
            overallScore: 50.0,
            grammarScore: 45.0,
            vocabularyScore: 60.0,
            coherenceScore: 65.0,
            taskCompletionScore: null,
            feedbackText: "Needs improvement.",
            correctedText: null);

        var signal = WritingDryRunSignalMapper.Map(eval);

        signal.Outcome.Should().Be(WritingDryRunSignalOutcome.CandidateReviewSignal);
        signal.SuggestedReviewNeed.Should().BeTrue();
        signal.AcceptedForFutureSignal.Should().BeTrue(); // Medium confidence
    }

    // ── Test 8: Failed → BlockedFailedEvaluation ──────────────────────────────

    [Fact]
    public void Map_Failed_BlockedFailedEvaluation()
    {
        var eval = MakeFailed();

        var signal = WritingDryRunSignalMapper.Map(eval);

        signal.Outcome.Should().Be(WritingDryRunSignalOutcome.BlockedFailedEvaluation);
        signal.IsBlocked.Should().BeTrue();
        signal.AcceptedForFutureSignal.Should().BeFalse();
        signal.BlockedReason.Should().NotBeNullOrEmpty();
    }

    // ── Test 9: NotSupported → BlockedUnsupportedProvider ────────────────────

    [Fact]
    public void Map_NotSupported_BlockedUnsupportedProvider()
    {
        var eval = MakeNotSupported();

        var signal = WritingDryRunSignalMapper.Map(eval);

        signal.Outcome.Should().Be(WritingDryRunSignalOutcome.BlockedUnsupportedProvider);
        signal.IsBlocked.Should().BeTrue();
        signal.AcceptedForFutureSignal.Should().BeFalse();
    }

    // ── Test 10: Missing overall_score → BlockedMissingScore ─────────────────

    [Fact]
    public void Map_CompletedNullOverallScore_BlockedMissingScore()
    {
        var eval = MakeCompleted(overallScore: null, grammarScore: 80.0, feedbackText: "ok", correctedText: "ok");

        var signal = WritingDryRunSignalMapper.Map(eval);

        signal.Outcome.Should().Be(WritingDryRunSignalOutcome.BlockedMissingScore);
        signal.IsBlocked.Should().BeTrue();
    }

    // ── Test 11: Missing corrected_text does NOT block CandidateReviewSignal ──

    [Fact]
    public void Map_ReviewSignal_NoCorrectedTextDoesNotBlock()
    {
        // overall <= 55 with at least 1 dimension + feedbackText = medium confidence review
        var eval = MakeCompleted(
            overallScore: 50.0,
            grammarScore: 48.0,
            vocabularyScore: null,
            coherenceScore: null,
            taskCompletionScore: null,
            feedbackText: "Needs improvement.",
            correctedText: null); // no corrected text

        var signal = WritingDryRunSignalMapper.Map(eval);

        // Should be CandidateReviewSignal, not blocked
        signal.Outcome.Should().Be(WritingDryRunSignalOutcome.CandidateReviewSignal);
        signal.IsBlocked.Should().BeFalse();
    }

    // ── Test 12: Low confidence → BlockedLowConfidence ────────────────────────

    [Fact]
    public void Map_LowConfidence_BlockedLowConfidence()
    {
        // Only overall score, no dimension scores, no feedback — Low confidence
        var eval = MakeCompleted(
            overallScore: 80.0,
            grammarScore: null,
            vocabularyScore: null,
            coherenceScore: null,
            taskCompletionScore: null,
            feedbackText: null,
            correctedText: null);

        var signal = WritingDryRunSignalMapper.Map(eval);

        signal.Outcome.Should().Be(WritingDryRunSignalOutcome.BlockedLowConfidence);
        signal.IsBlocked.Should().BeTrue();
        signal.ConfidenceBand.Should().Be(WritingDryRunConfidenceBand.Low);
    }

    // ── Test 13: Mapper has no side effects on mastery ─────────────────────────

    [Fact]
    public void Map_NeverModifiesMastery_IsDryRunOnly()
    {
        // The mapper must return IsDryRunOnly=true for all signal outcomes.
        var evals = new List<WritingEvaluation>
        {
            MakeCompleted(),
            MakeFailed(),
            MakeNotSupported(),
            MakePending(),
        };

        foreach (var eval in evals)
        {
            var signal = WritingDryRunSignalMapper.Map(eval);
            signal.IsDryRunOnly.Should().BeTrue(
                because: "writing dry-run signals must never be applied to mastery");
        }
    }

    // ── Test 14: Mapper returns no CEFR update ─────────────────────────────────

    [Fact]
    public void Map_NoCefrUpdateField()
    {
        // WritingEvaluationDryRunSignal has no CEFR update field.
        var eval = MakeCompleted();
        var signal = WritingDryRunSignalMapper.Map(eval);

        // Signal type has no UpdateCefr, CefrDelta, or similar property.
        var type = signal.GetType();
        type.GetProperty("UpdateCefr").Should().BeNull();
        type.GetProperty("CefrDelta").Should().BeNull();
        type.GetProperty("NewCefrLevel").Should().BeNull();
    }

    // ── Test 15: Mapper returns no objective completion ────────────────────────

    [Fact]
    public void Map_NoObjectiveCompletionField()
    {
        // WritingEvaluationDryRunSignal has no objective completion field.
        var eval = MakeCompleted();
        var signal = WritingDryRunSignalMapper.Map(eval);

        var type = signal.GetType();
        type.GetProperty("CompleteObjective").Should().BeNull();
        type.GetProperty("ObjectiveIds").Should().BeNull();
        type.GetProperty("CompletedObjectives").Should().BeNull();
    }
}
