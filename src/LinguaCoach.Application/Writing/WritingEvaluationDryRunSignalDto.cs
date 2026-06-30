namespace LinguaCoach.Application.Writing;

/// <summary>
/// Serializable DTO for a writing evaluation dry-run signal.
/// Returned by the admin API. Never reflects applied mastery changes.
/// </summary>
public sealed class WritingEvaluationDryRunSignalDto
{
    public Guid EvaluationId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid StudentId { get; init; }
    public Guid ActivityId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public string SourceStatus { get; init; } = string.Empty;
    public string CandidateSkill { get; init; } = "Writing";
    public double? OverallScore { get; init; }
    public double? GrammarScore { get; init; }
    public double? VocabularyScore { get; init; }
    public double? CoherenceScore { get; init; }
    public double? TaskCompletionScore { get; init; }
    public string ConfidenceBand { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public double? SuggestedMasteryDelta { get; init; }
    public bool SuggestedReviewNeed { get; init; }
    public bool AcceptedForFutureSignal { get; init; }
    public string? BlockedReason { get; init; }
    public string? Notes { get; init; }
}
