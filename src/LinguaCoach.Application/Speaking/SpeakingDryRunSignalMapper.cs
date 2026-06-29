using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Maps a SpeakingEvaluation to a dry-run signal preview.
/// Pure logic — no DB, no side effects. Never updates mastery, CEFR, or Learning Plan.
/// </summary>
public static class SpeakingDryRunSignalMapper
{
    private const double PositiveScoreThreshold = 70.0;
    private const double ReviewScoreThreshold = 40.0;
    private const double DimensionMinThreshold = 50.0;

    public static SpeakingEvaluationDryRunSignal Map(SpeakingEvaluation evaluation)
    {
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

        var score = evaluation.OverallScore.Value;
        var completenessOk = evaluation.CompletenessScore is null
            || evaluation.CompletenessScore >= DimensionMinThreshold;
        var relevanceOk = evaluation.RelevanceScore is null
            || evaluation.RelevanceScore >= DimensionMinThreshold;

        if (score >= PositiveScoreThreshold && completenessOk && relevanceOk)
            return new SpeakingEvaluationDryRunSignal(evalId, attemptId, confidence,
                SpeakingDryRunSignalOutcome.CandidatePositiveSignal,
                CandidateSkill: "Speaking", BlockedReason: null);

        if (score >= ReviewScoreThreshold)
            return new SpeakingEvaluationDryRunSignal(evalId, attemptId, confidence,
                SpeakingDryRunSignalOutcome.CandidateReviewSignal,
                CandidateSkill: "Speaking", BlockedReason: null);

        return new SpeakingEvaluationDryRunSignal(evalId, attemptId, confidence,
            SpeakingDryRunSignalOutcome.CandidateNoSignal,
            CandidateSkill: null, BlockedReason: null);
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
        string? feedbackText)
    {
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

        var score = overallScore.Value;
        var completenessOk = completenessScore is null || completenessScore >= DimensionMinThreshold;
        var relevanceOk    = relevanceScore is null    || relevanceScore >= DimensionMinThreshold;

        if (score >= PositiveScoreThreshold && completenessOk && relevanceOk)
            return new SpeakingEvaluationDryRunSignal(evalId, attemptId, confidence,
                SpeakingDryRunSignalOutcome.CandidatePositiveSignal,
                CandidateSkill: "Speaking", BlockedReason: null);

        if (score >= ReviewScoreThreshold)
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
