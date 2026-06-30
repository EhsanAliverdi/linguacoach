namespace LinguaCoach.Application.Writing;

/// <summary>
/// Applies high-confidence, config-gated writing evaluation signals to student learning state.
/// Conservative by design — disabled by default, CEFR/objective-completion always off.
/// </summary>
public interface IWritingEvaluationSignalApplicationService
{
    /// <summary>
    /// Processes completed evaluations with candidate dry-run signals.
    /// Applies allowed signals, writes audit records, skips duplicates.
    /// Returns a summary of outcomes for the batch.
    /// </summary>
    Task<WritingSignalApplicationBatchResult> ApplyPendingSignalsAsync(int maxBatch, CancellationToken ct = default);

    /// <summary>Returns counts of applied, dry-run-only, and blocked signals across all evaluations.</summary>
    Task<WritingSignalApplicationSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns invariant safety verification.
    /// Confirms CEFR updates, objective completions, and LP auto-regen are structurally disabled.
    /// </summary>
    Task<WritingSignalSafetySummaryDto> GetSignalSafetySummaryAsync(CancellationToken ct = default);
}

public sealed record WritingSignalApplicationBatchResult(
    int Processed,
    int Applied,
    int BlockedByConfig,
    int BlockedByConfidence,
    int BlockedBySignalType,
    int DuplicateSkipped,
    int NoSignal,
    int Failed);

public sealed record WritingSignalApplicationSummaryDto(
    bool MasteryIntegrationEnabled,
    bool ReviewSignalsAllowed,
    bool PositiveSignalsAllowed,
    bool ObjectiveCompletionAllowed,
    bool CefrUpdateAllowed,
    string MinimumConfidenceRequired,
    int TotalCompletedEvaluations,
    int CandidateSignals,
    int AppliedSignals,
    int BlockedByConfig,
    int BlockedByConfidence,
    int BlockedBySignalType,
    int BlockedByFailedOrUnsupported,
    int BlockedByMissingScore,
    int DuplicateSkipped,
    int NoSignal,
    int FailedApplication);

public sealed record WritingSignalSafetySummaryDto(
    bool CefrUpdatesDisabled,
    bool ObjectiveCompletionsDisabled,
    bool LearningPlanAutoRegenDisabled,
    bool SignalApplicationEnabled,
    bool PositiveSignalsEnabled,
    bool ReviewSignalsEnabled,
    int TotalApplied,
    int PositiveApplied,
    int ReviewApplied,
    bool InvariantViolationsDetected);
