using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Maps a SpeakingEvaluation to a dry-run signal preview.
/// Pure logic — no DB, no side effects. Never updates mastery, CEFR, or Learning Plan.
/// Pass explicit thresholds to override defaults; null falls back to SpeakingSignalThresholds.Default.
/// </summary>
public static class SpeakingDryRunSignalMapper
{
    public static SpeakingEvaluationDryRunSignal Map(
        SpeakingEvaluation evaluation,
        SpeakingSignalThresholds? thresholds = null)
    {
        var t = thresholds ?? SpeakingSignalThresholds.Default;
        var evalId = evaluation.Id;
        var attemptId = evaluation.ActivityAttemptId;

        switch (evaluation.Status)
        {
            case SpeakingEvaluationStatus.Failed:
                return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedFailedEvaluation,
                    "Evaluation failed.", null);

            case SpeakingEvaluationStatus.NotSupported:
            case SpeakingEvaluationStatus.Skipped:
                return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedUnsupportedProvider,
                    "Provider not supported.", null);

            case SpeakingEvaluationStatus.Pending:
            case SpeakingEvaluationStatus.Evaluating:
                return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedInsufficientData,
                    "Evaluation not yet complete.", null);

            case SpeakingEvaluationStatus.Completed:
                break;

            default:
                return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedInsufficientData,
                    "Unknown evaluation status.", null);
        }

        if (evaluation.OverallScore is null)
            return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedMissingScore,
                "OverallScore not present.", null);

        var confidence = ComputeConfidence(evaluation);

        if (confidence == SpeakingDryRunConfidenceBand.Low)
            return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedLowConfidence,
                "Confidence too low for candidate signal.", confidence);

        return ClassifyScore(evalId, attemptId, confidence,
            evaluation.OverallScore.Value,
            evaluation.CompletenessScore,
            evaluation.RelevanceScore, t);
    }

    /// <summary>
    /// Overload for use when only projected scalar fields are available (no full entity).
    /// Produces identical results to Map(SpeakingEvaluation).
    /// </summary>
    public static SpeakingEvaluationDryRunSignal MapFromFields(
        Guid evalId,
        Guid attemptId,
        SpeakingEvaluationStatus status,
        double? overallScore,
        double? fluencyScore,
        double? completenessScore,
        double? relevanceScore,
        string? feedbackText,
        SpeakingSignalThresholds? thresholds = null)
    {
        var t = thresholds ?? SpeakingSignalThresholds.Default;

        switch (status)
        {
            case SpeakingEvaluationStatus.Failed:
                return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedFailedEvaluation,
                    "Evaluation failed.", null);

            case SpeakingEvaluationStatus.NotSupported:
            case SpeakingEvaluationStatus.Skipped:
                return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedUnsupportedProvider,
                    "Provider not supported.", null);

            case SpeakingEvaluationStatus.Pending:
            case SpeakingEvaluationStatus.Evaluating:
                return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedInsufficientData,
                    "Evaluation not yet complete.", null);

            case SpeakingEvaluationStatus.Completed:
                break;

            default:
                return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedInsufficientData,
                    "Unknown evaluation status.", null);
        }

        if (overallScore is null)
            return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedMissingScore,
                "OverallScore not present.", null);

        var confidence = ComputeConfidenceFromFields(overallScore, fluencyScore, completenessScore, relevanceScore, feedbackText);

        if (confidence == SpeakingDryRunConfidenceBand.Low)
            return Blocked(evalId, attemptId, SpeakingDryRunSignalOutcome.BlockedLowConfidence,
                "Confidence too low for candidate signal.", confidence);

        return ClassifyScore(evalId, attemptId, confidence,
            overallScore.Value, completenessScore, relevanceScore, t);
    }

    private static SpeakingEvaluationDryRunSignal ClassifyScore(
        Guid evalId, Guid attemptId,
        SpeakingDryRunConfidenceBand confidence,
        double score,
        double? completenessScore,
        double? relevanceScore,
        SpeakingSignalThresholds t)
    {
        var completenessOk = completenessScore is null || completenessScore >= t.MinPositiveCompleteness;
        var relevanceOk    = relevanceScore is null    || relevanceScore >= t.MinPositiveRelevance;

        if (score >= t.MinPositiveOverall && completenessOk && relevanceOk)
            return new SpeakingEvaluationDryRunSignal(evalId, attemptId, confidence,
                SpeakingDryRunSignalOutcome.CandidatePositiveSignal,
                CandidateSkill: "Speaking", BlockedReason: null);

        if (score <= t.MaxReviewOverall)
            return new SpeakingEvaluationDryRunSignal(evalId, attemptId, confidence,
                SpeakingDryRunSignalOutcome.CandidateReviewSignal,
                CandidateSkill: "Speaking", BlockedReason: null);

        return new SpeakingEvaluationDryRunSignal(evalId, attemptId, confidence,
            SpeakingDryRunSignalOutcome.CandidateNoSignal,
            CandidateSkill: null, BlockedReason: null);
    }

    private static SpeakingDryRunConfidenceBand ComputeConfidence(SpeakingEvaluation evaluation) =>
        ComputeConfidenceFromFields(
            evaluation.OverallScore,
            evaluation.FluencyScore,
            evaluation.CompletenessScore,
            evaluation.RelevanceScore,
            evaluation.FeedbackText);

    private static SpeakingDryRunConfidenceBand ComputeConfidenceFromFields(
        double? overallScore,
        double? fluencyScore,
        double? completenessScore,
        double? relevanceScore,
        string? feedbackText)
    {
        var score = 0;
        if (overallScore is not null) score++;
        if (fluencyScore is not null || completenessScore is not null || relevanceScore is not null) score++;
        if (feedbackText is not null) score++;

        return score switch
        {
            >= 3 => SpeakingDryRunConfidenceBand.High,
            2    => SpeakingDryRunConfidenceBand.Medium,
            _    => SpeakingDryRunConfidenceBand.Low,
        };
    }

    private static SpeakingEvaluationDryRunSignal Blocked(
        Guid evalId, Guid attemptId,
        SpeakingDryRunSignalOutcome outcome,
        string reason,
        SpeakingDryRunConfidenceBand? confidence) =>
        new(evalId, attemptId, confidence, outcome, CandidateSkill: null, BlockedReason: reason);
}
