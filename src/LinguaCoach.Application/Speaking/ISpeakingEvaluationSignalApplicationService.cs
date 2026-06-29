namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Applies high-confidence, config-gated speaking evaluation signals to student learning state.
/// Conservative by design — disabled by default, CEFR/objective-completion always off.
/// </summary>
public interface ISpeakingEvaluationSignalApplicationService
{
    /// <summary>
    /// Processes completed evaluations with candidate dry-run signals.
    /// Applies allowed signals, writes audit records, skips duplicates.
    /// Returns a summary of outcomes for the batch.
    /// </summary>
    Task<SpeakingSignalApplicationBatchResult> ApplyPendingSignalsAsync(int maxBatch, CancellationToken ct = default);

    /// <summary>Returns counts of applied, dry-run-only, and blocked signals across all evaluations.</summary>
    Task<SpeakingSignalApplicationSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}

public sealed record SpeakingSignalApplicationBatchResult(
    int Processed,
    int Applied,
    int BlockedByConfig,
    int BlockedByConfidence,
    int BlockedBySignalType,
    int DuplicateSkipped,
    int NoSignal,
    int Failed);

public sealed record SpeakingSignalApplicationSummaryDto(
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
