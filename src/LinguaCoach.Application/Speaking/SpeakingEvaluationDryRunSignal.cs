using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Dry-run only. Never applied to mastery, CEFR, or Learning Plan progress.
/// Represents what a learning signal could look like if evaluation results were trusted.
/// Computed on demand — not persisted. Admin-visible only.
/// </summary>
public sealed record SpeakingEvaluationDryRunSignal(
    Guid EvaluationId,
    Guid AttemptId,
    SpeakingDryRunConfidenceBand? ConfidenceBand,
    SpeakingDryRunSignalOutcome Outcome,
    string? CandidateSkill,
    string? BlockedReason)
{
    public bool IsDryRunOnly => true;

    public bool IsCandidate =>
        Outcome == SpeakingDryRunSignalOutcome.CandidatePositiveSignal ||
        Outcome == SpeakingDryRunSignalOutcome.CandidateReviewSignal;

    public bool IsBlocked =>
        Outcome is SpeakingDryRunSignalOutcome.BlockedMissingScore
            or SpeakingDryRunSignalOutcome.BlockedFailedEvaluation
            or SpeakingDryRunSignalOutcome.BlockedUnsupportedProvider
            or SpeakingDryRunSignalOutcome.BlockedLowConfidence
            or SpeakingDryRunSignalOutcome.BlockedInsufficientData;
}
