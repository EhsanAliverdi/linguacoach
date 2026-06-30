namespace LinguaCoach.Application.Writing;

public sealed record WritingEvaluationDto(
    Guid AttemptId,
    /// <summary>Pending | Evaluating | Completed | Failed | Skipped | NotSupported</summary>
    string Status,
    string? FeedbackText,
    string? SuggestedImprovement,
    string? CorrectedText,
    double? OverallScore,
    double? GrammarScore,
    double? VocabularyScore,
    double? CoherenceScore,
    double? TaskCompletionScore,
    DateTime? CompletedAtUtc,
    string? FailureReason,
    string? ProviderName,
    string? ModelName);
