namespace LinguaCoach.Application.Writing;

/// <summary>
/// Admin DTO combining a writing evaluation record with its dry-run signal preview.
/// Dry-run signal is never applied to mastery, CEFR, or Learning Plan.
/// </summary>
public sealed class WritingEvaluationWithDryRunDto
{
    public Guid EvaluationId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid StudentId { get; init; }
    public Guid ActivityId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public double? OverallScore { get; init; }
    public double? GrammarScore { get; init; }
    public double? VocabularyScore { get; init; }
    public double? CoherenceScore { get; init; }
    public double? TaskCompletionScore { get; init; }
    public string? FeedbackText { get; init; }
    public string? SuggestedImprovement { get; init; }
    public string? CorrectedText { get; init; }
    public string? FailureReason { get; init; }
    public WritingEvaluationDryRunSignalDto? DryRunSignal { get; init; }
}
