namespace LinguaCoach.Application.Speaking;

public sealed record SpeakingEvaluationQualitySummaryDto(
    int Total,
    int Completed,
    int Failed,
    int NotSupported,
    int Pending,
    double CompletionRate,
    double FailureRate,
    double? AverageOverallScore,
    double? AverageFluencyScore,
    double? AverageCompletenessScore,
    double? AverageRelevanceScore,
    double? AveragePronunciationScore,
    double NullOverallScoreRate,
    double NullFluencyScoreRate,
    double NullCompletenessScoreRate,
    double NullRelevanceScoreRate,
    int DryRunCandidatePositiveSignals,
    int DryRunCandidateReviewSignals,
    int DryRunCandidateNoSignals,
    int DryRunBlocked,
    // Phase 16J — applied / blocked breakdown
    int DryRunCandidates,
    int Applied,
    int BlockedByConfig,
    int BlockedByConfidence,
    int BlockedByMissingScore,
    int BlockedByUnsupportedStatus,
    int BlockedByFailedEval,
    int DuplicateSkipped,
    int AppliedReview,
    int AppliedPositive,
    IReadOnlyList<SpeakingProviderModelCount> ProviderModelDistribution,
    IReadOnlyList<string> LatestFailureReasons,
    IReadOnlyList<string> LatestBlockedReasons);

public sealed record SpeakingProviderModelCount(
    string ProviderName,
    string? ModelName,
    int Count);
