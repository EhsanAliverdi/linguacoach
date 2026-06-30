using FluentAssertions;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Speaking;

/// <summary>
/// Unit tests for SpeakingDryRunSignalMapper.
/// Phase 16H — dry-run signal mapping rules.
/// Mastery, CEFR, and Learning Plan are never modified.
/// </summary>
public sealed class SpeakingDryRunSignalMapperTests
{
    // ── Status gates ──────────────────────────────────────────────────────────

    [Fact]
    public void FailedEvaluation_ProducesBlockedFailedEvaluation()
    {
        var signal = MapFromFields(status: SpeakingEvaluationStatus.Failed, overallScore: 80);
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.BlockedFailedEvaluation);
        signal.IsBlocked.Should().BeTrue();
        signal.IsCandidate.Should().BeFalse();
    }

    [Fact]
    public void NotSupportedEvaluation_ProducesBlockedUnsupportedProvider()
    {
        var signal = MapFromFields(status: SpeakingEvaluationStatus.NotSupported, overallScore: 80);
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.BlockedUnsupportedProvider);
        signal.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void PendingEvaluation_ProducesBlockedInsufficientData()
    {
        var signal = MapFromFields(status: SpeakingEvaluationStatus.Pending, overallScore: null);
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.BlockedInsufficientData);
        signal.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void EvaluatingStatus_ProducesBlockedInsufficientData()
    {
        var signal = MapFromFields(status: SpeakingEvaluationStatus.Evaluating, overallScore: null);
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.BlockedInsufficientData);
    }

    // ── Missing score gate ─────────────────────────────────────────────────────

    [Fact]
    public void CompletedEvaluation_NullOverallScore_ProducesBlockedMissingScore()
    {
        var signal = MapFromFields(status: SpeakingEvaluationStatus.Completed, overallScore: null);
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.BlockedMissingScore);
        signal.IsBlocked.Should().BeTrue();
    }

    // ── Confidence gate ───────────────────────────────────────────────────────

    [Fact]
    public void CompletedEvaluation_OverallScoreOnly_NoFeedback_NoDimensions_IsLowConfidence_Blocked()
    {
        // Only OverallScore present → confidence score = 1 → Low → blocked
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 80,
            fluencyScore: null,
            completenessScore: null,
            relevanceScore: null,
            feedbackText: null);

        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.BlockedLowConfidence);
        signal.ConfidenceBand.Should().Be(SpeakingDryRunConfidenceBand.Low);
    }

    [Fact]
    public void CompletedEvaluation_OverallAndOneDimension_MediumConfidence()
    {
        // OverallScore + fluencyScore = 2 → Medium confidence
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 80,
            fluencyScore: 72,
            completenessScore: null,
            relevanceScore: null,
            feedbackText: null);

        signal.ConfidenceBand.Should().Be(SpeakingDryRunConfidenceBand.Medium);
    }

    [Fact]
    public void CompletedEvaluation_FullScoreAndFeedback_HighConfidence()
    {
        // OverallScore + dimension + feedback = 3 → High confidence
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 78,
            fluencyScore: 72,
            completenessScore: 80,
            relevanceScore: 75,
            feedbackText: "Good effort.");

        signal.ConfidenceBand.Should().Be(SpeakingDryRunConfidenceBand.High);
    }

    // ── Signal outcomes ───────────────────────────────────────────────────────

    [Fact]
    public void StrongScores_HighConfidence_ProducesCandidatePositiveSignal()
    {
        // Phase 16J thresholds: overall≥80, completeness≥80, relevance≥80
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 85,
            fluencyScore: 82,
            completenessScore: 82,
            relevanceScore: 81,
            feedbackText: "Great speaking.");

        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
        signal.CandidateSkill.Should().Be("Speaking");
        signal.IsCandidate.Should().BeTrue();
        signal.IsDryRunOnly.Should().BeTrue();
    }

    [Fact]
    public void MidRangeScore_MediumConfidence_ProducesCandidateReviewSignal()
    {
        // Phase 16J: score=55 ≤ MaxReviewOverall(55) → CandidateReviewSignal
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 55,
            fluencyScore: 50,
            completenessScore: null,
            relevanceScore: null,
            feedbackText: null);

        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateReviewSignal);
        signal.CandidateSkill.Should().Be("Speaking");
        signal.IsCandidate.Should().BeTrue();
    }

    [Fact]
    public void LowScore_MediumConfidence_ProducesCandidateNoSignal()
    {
        // Phase 16J: NoSignal = score in middle band (56–79): above MaxReview(55), below MinPositive(80)
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 65,
            fluencyScore: 60,
            completenessScore: null,
            relevanceScore: null,
            feedbackText: null);

        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateNoSignal);
        signal.CandidateSkill.Should().BeNull();
        signal.IsCandidate.Should().BeFalse();
        signal.IsBlocked.Should().BeFalse();
    }

    // ── Completeness/relevance dimension gates ────────────────────────────────

    [Fact]
    public void StrongOverall_LowCompleteness_BlocksPositiveSignal_ProducesNoSignal()
    {
        // Phase 16J: overall≥80 but completeness=30 < 80 → blocks positive.
        // Score 85 > MaxReview(55) → NoSignal (middle band), not Review.
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 85,
            fluencyScore: 80,
            completenessScore: 30,
            relevanceScore: null,
            feedbackText: "Feedback.");

        signal.Outcome.Should().NotBe(SpeakingDryRunSignalOutcome.CandidatePositiveSignal,
            "low completeness score should prevent positive signal");
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateNoSignal,
            "score above MaxReviewOverall(55) puts evaluation in middle band, not review");
    }

    [Fact]
    public void StrongOverall_LowRelevance_BlocksPositiveSignal_ProducesNoSignal()
    {
        // Phase 16J: overall≥80 but relevance=20 < 80 → blocks positive.
        // Score 85 > MaxReview(55) → NoSignal.
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 85,
            fluencyScore: 80,
            completenessScore: null,
            relevanceScore: 20,
            feedbackText: "Feedback.");

        signal.Outcome.Should().NotBe(SpeakingDryRunSignalOutcome.CandidatePositiveSignal,
            "low relevance score should prevent positive signal");
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateNoSignal,
            "score above MaxReviewOverall(55) puts evaluation in middle band, not review");
    }

    [Fact]
    public void MissingPronunciationScore_DoesNotBlockGeneralFeedbackSignal()
    {
        // PronunciationScore is not tracked in the mapper — missing it should not block.
        // Phase 16J thresholds: overall≥80, completeness≥80, relevance≥80.
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 85,
            fluencyScore: 82,
            completenessScore: 82,
            relevanceScore: 81,
            feedbackText: "Good effort.");

        // CandidatePositiveSignal even without pronunciation score
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
    }

    // ── Dry-run never updates mastery ─────────────────────────────────────────

    [Fact]
    public void Signal_IsDryRunOnly_AlwaysTrue()
    {
        var positive = MapFromFields(SpeakingEvaluationStatus.Completed, overallScore: 80,
            fluencyScore: 75, completenessScore: 80, feedbackText: "Good.");
        var review = MapFromFields(SpeakingEvaluationStatus.Completed, overallScore: 50,
            fluencyScore: 45, completenessScore: null, feedbackText: null);
        var blocked = MapFromFields(SpeakingEvaluationStatus.Failed, overallScore: null);

        positive.IsDryRunOnly.Should().BeTrue();
        review.IsDryRunOnly.Should().BeTrue();
        blocked.IsDryRunOnly.Should().BeTrue();
    }

    // ── Map(SpeakingEvaluation) overload ──────────────────────────────────────

    [Fact]
    public void Map_Entity_ProducesSameResultAsMapFromFields()
    {
        var evalId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var eval = SpeakingEvaluation.CreatePending(attemptId, studentId, activityId);
        eval.MarkEvaluating("fake", "fake-model");
        eval.MarkCompleted(
            transcript: "test",
            overallScore: 78,
            fluencyScore: 72,
            pronunciationScore: null,
            completenessScore: 80,
            relevanceScore: 75,
            feedbackText: "Great work.",
            suggestedImprovement: "Try again.");

        var fromEntity = SpeakingDryRunSignalMapper.Map(eval);
        var fromFields = SpeakingDryRunSignalMapper.MapFromFields(
            evalId: eval.Id,
            attemptId: attemptId,
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 78,
            fluencyScore: 72,
            completenessScore: 80,
            relevanceScore: 75,
            feedbackText: "Great work.");

        fromEntity.Outcome.Should().Be(fromFields.Outcome);
        fromEntity.ConfidenceBand.Should().Be(fromFields.ConfidenceBand);
        fromEntity.CandidateSkill.Should().Be(fromFields.CandidateSkill);
    }

    // ── Phase 16J — threshold-aware tests ────────────────────────────────────

    [Fact]
    public void ScoreAtPositiveThreshold_ProducesCandidatePositiveSignal()
    {
        // score=80 exactly at MinPositiveOverall(80), all dims pass
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 80,
            fluencyScore: 80,
            completenessScore: 80,
            relevanceScore: 80,
            feedbackText: "Good.");
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
    }

    [Fact]
    public void ScoreJustBelowPositiveThreshold_DoesNotProducePositive()
    {
        // score=79.9 < 80 → not positive; 79.9 > 55 → NoSignal
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 79.9,
            fluencyScore: 80,
            completenessScore: 80,
            relevanceScore: 80,
            feedbackText: "Good.");
        signal.Outcome.Should().NotBe(SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateNoSignal);
    }

    [Fact]
    public void ScoreAtReviewMaxThreshold_ProducesCandidateReviewSignal()
    {
        // score=55 exactly at MaxReviewOverall(55) → Review
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 55,
            fluencyScore: 50,
            completenessScore: null,
            relevanceScore: null,
            feedbackText: "Needs work.");
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateReviewSignal);
    }

    [Fact]
    public void ScoreJustAboveReviewMax_ProducesCandidateNoSignal()
    {
        // score=56 > 55 (above MaxReview) and < 80 (below MinPositive) → NoSignal
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 56,
            fluencyScore: 55,
            completenessScore: null,
            relevanceScore: null,
            feedbackText: "Feedback.");
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateNoSignal);
    }

    [Fact]
    public void LowCompleteness_BlocksPositiveSignal_WithNewThreshold()
    {
        // overall=85 ≥ 80 but completeness=79 < 80 → blocks positive → NoSignal
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 85,
            fluencyScore: 82,
            completenessScore: 79,
            relevanceScore: 82,
            feedbackText: "Good.");
        signal.Outcome.Should().NotBe(SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateNoSignal);
    }

    [Fact]
    public void LowRelevance_BlocksPositiveSignal_WithNewThreshold()
    {
        // overall=85 ≥ 80 but relevance=79 < 80 → blocks positive → NoSignal
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 85,
            fluencyScore: 82,
            completenessScore: 82,
            relevanceScore: 79,
            feedbackText: "Good.");
        signal.Outcome.Should().NotBe(SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateNoSignal);
    }

    [Fact]
    public void CustomThresholds_AreRespected()
    {
        // Pass explicit legacy-like thresholds: positive ≥70, review ≤40
        var legacyThresholds = new SpeakingSignalThresholds(
            MinPositiveOverall: 70, MinPositiveRelevance: 50, MinPositiveCompleteness: 50,
            MaxReviewOverall: 40, MaxReviewRelevance: 40, MaxReviewCompleteness: 40);

        var signal = SpeakingDryRunSignalMapper.MapFromFields(
            evalId: Guid.NewGuid(),
            attemptId: Guid.NewGuid(),
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 75,
            fluencyScore: 70,
            completenessScore: 70,
            relevanceScore: 70,
            feedbackText: "Good.",
            thresholds: legacyThresholds);

        // With legacy thresholds 75 ≥ 70 → Positive
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SpeakingEvaluationDryRunSignal MapFromFields(
        SpeakingEvaluationStatus status,
        double? overallScore,
        double? fluencyScore = null,
        double? completenessScore = null,
        double? relevanceScore = null,
        string? feedbackText = null) =>
        SpeakingDryRunSignalMapper.MapFromFields(
            evalId: Guid.NewGuid(),
            attemptId: Guid.NewGuid(),
            status: status,
            overallScore: overallScore,
            fluencyScore: fluencyScore,
            completenessScore: completenessScore,
            relevanceScore: relevanceScore,
            feedbackText: feedbackText);
}
