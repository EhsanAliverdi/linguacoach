namespace LinguaCoach.Application.Speaking;

public sealed record SpeakingEvaluationDto(
    Guid AttemptId,
    /// <summary>Pending | Evaluating | Completed | Failed | Skipped | NotSupported</summary>
    string Status,
    string? FeedbackText,
    string? SuggestedImprovement,
    string? Transcript,
    double? OverallScore,
    double? FluencyScore,
    double? PronunciationScore,
    double? CompletenessScore,
    double? RelevanceScore,
    DateTime? CompletedAtUtc,
    string? FailureReason,
    string? ProviderName,
    string? ModelName);
