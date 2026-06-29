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
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 78,
            fluencyScore: 72,
            completenessScore: 80,
            relevanceScore: 75,
            feedbackText: "Great speaking.");

        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
        signal.CandidateSkill.Should().Be("Speaking");
        signal.IsCandidate.Should().BeTrue();
        signal.IsDryRunOnly.Should().BeTrue();
    }

    [Fact]
    public void MidRangeScore_MediumConfidence_ProducesCandidateReviewSignal()
    {
        // Score 55 ≥ ReviewThreshold(40), < PositiveThreshold(70)
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
        // Score 30 < ReviewThreshold(40)
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 30,
            fluencyScore: 25,
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
    public void StrongOverall_LowCompleteness_ProducesCandidateReviewSignal_NotPositive()
    {
        // OverallScore 75 ≥ 70 but CompletenessScore 30 < 50 threshold → not positive
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 75,
            fluencyScore: 70,
            completenessScore: 30,
            relevanceScore: null,
            feedbackText: "Feedback.");

        signal.Outcome.Should().NotBe(SpeakingDryRunSignalOutcome.CandidatePositiveSignal,
            "low completeness score should prevent positive signal");
        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateReviewSignal);
    }

    [Fact]
    public void StrongOverall_LowRelevance_ProducesCandidateReviewSignal_NotPositive()
    {
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 75,
            fluencyScore: 70,
            completenessScore: null,
            relevanceScore: 20,
            feedbackText: "Feedback.");

        signal.Outcome.Should().Be(SpeakingDryRunSignalOutcome.CandidateReviewSignal);
    }

    [Fact]
    public void MissingPronunciationScore_DoesNotBlockGeneralFeedbackSignal()
    {
        // PronunciationScore is not tracked in the mapper — missing it should not block
        var signal = MapFromFields(
            status: SpeakingEvaluationStatus.Completed,
            overallScore: 78,
            fluencyScore: 72,
            completenessScore: 80,
            relevanceScore: 75,
            feedbackText: "Good effort.");

        // Should still be CandidatePositiveSignal even without pronunciation score
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
