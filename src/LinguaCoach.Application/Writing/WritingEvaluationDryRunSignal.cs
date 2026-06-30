using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Writing;

/// <summary>
/// Dry-run only. Never applied to mastery, CEFR, or Learning Plan progress.
/// Represents what a learning signal could look like if writing evaluation results were trusted.
/// Computed on demand — not persisted. Admin-visible only.
/// </summary>
public sealed class WritingEvaluationDryRunSignal
{
    public Guid EvaluationId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid StudentId { get; init; }
    public Guid ActivityId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public WritingEvaluationStatus SourceStatus { get; init; }
    public string CandidateSkill { get; init; } = "Writing";
    public double? OverallScore { get; init; }
    public double? GrammarScore { get; init; }
    public double? VocabularyScore { get; init; }
    public double? CoherenceScore { get; init; }
    public double? TaskCompletionScore { get; init; }
    public WritingDryRunConfidenceBand ConfidenceBand { get; init; }
    public WritingDryRunSignalOutcome Outcome { get; init; }
    public double? SuggestedMasteryDelta { get; init; }
    public bool SuggestedReviewNeed { get; init; }
    public bool AcceptedForFutureSignal { get; init; }
    public string? BlockedReason { get; init; }
    public string? Notes { get; init; }

    /// <summary>Always true — this signal is never applied to mastery.</summary>
    public bool IsDryRunOnly => true;

    public bool IsCandidate =>
        Outcome == WritingDryRunSignalOutcome.CandidatePositiveSignal ||
        Outcome == WritingDryRunSignalOutcome.CandidateReviewSignal;

    public bool IsBlocked =>
        Outcome is WritingDryRunSignalOutcome.BlockedMissingScore
            or WritingDryRunSignalOutcome.BlockedFailedEvaluation
            or WritingDryRunSignalOutcome.BlockedUnsupportedProvider
            or WritingDryRunSignalOutcome.BlockedLowConfidence
            or WritingDryRunSignalOutcome.BlockedInsufficientData;
}
