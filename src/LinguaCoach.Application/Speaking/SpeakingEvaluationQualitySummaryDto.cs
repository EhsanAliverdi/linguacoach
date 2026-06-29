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
    double NullOverallScoreRate,
    double NullFluencyScoreRate,
    double NullCompletenessScoreRate,
    double NullRelevanceScoreRate,
    int DryRunCandidatePositiveSignals,
    int DryRunCandidateReviewSignals,
    int DryRunCandidateNoSignals,
    int DryRunBlocked,
    IReadOnlyList<string> LatestFailureReasons);
