namespace LinguaCoach.Application.Writing;

/// <summary>
/// Admin-only quality summary for the writing evaluation pipeline.
/// Dry-run only — signals are never applied to mastery, CEFR, or Learning Plan.
/// </summary>
public sealed class WritingEvaluationQualitySummaryDto
{
    public bool ConfigEnabled { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public int TotalEvaluations { get; init; }
    public int PendingCount { get; init; }
    public int EvaluatingCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public int NotSupportedCount { get; init; }
    public double CompletionRate { get; init; }
    public double FailureRate { get; init; }
    public double NullOverallScoreRate { get; init; }
    public double NullGrammarScoreRate { get; init; }
    public double NullVocabularyScoreRate { get; init; }
    public double NullCoherenceScoreRate { get; init; }
    public double NullTaskCompletionScoreRate { get; init; }
    public double CorrectedTextAvailabilityRate { get; init; }
    public double? AverageOverallScore { get; init; }
    public double? AverageGrammarScore { get; init; }
    public double? AverageVocabularyScore { get; init; }
    public double? AverageCoherenceScore { get; init; }
    public double? AverageTaskCompletionScore { get; init; }
    public int DryRunCandidateCount { get; init; }
    public int DryRunBlockedCount { get; init; }
    public Dictionary<string, int> DryRunOutcomeBreakdown { get; init; } = new();
    public List<string> LatestFailureReasons { get; init; } = new();
    public string Note { get; init; } = "Dry-run only — not applied to mastery";
}
