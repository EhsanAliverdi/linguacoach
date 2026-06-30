namespace LinguaCoach.Application.Writing;

/// <summary>
/// Admin read-only query over a student's writing evaluations.
/// Returns activity context and evaluation results. Never exposes raw provider payloads.
/// </summary>
public interface IAdminWritingEvaluationQuery
{
    Task<IReadOnlyList<AdminWritingEvaluationItemDto>> GetForStudentAsync(
        Guid studentProfileId,
        CancellationToken ct = default);
}

public sealed record AdminWritingEvaluationItemDto(
    Guid EvaluationId,
    Guid AttemptId,
    Guid ActivityId,
    string? ActivityTitle,
    string? ActivityType,
    string Status,
    string? ProviderName,
    string? ModelName,
    DateTime? SubmittedAtUtc,
    DateTime? CompletedAtUtc,
    double? OverallScore,
    double? GrammarScore,
    double? VocabularyScore,
    double? CoherenceScore,
    double? TaskCompletionScore,
    string? FeedbackText,
    string? SuggestedImprovement,
    string? CorrectedText,
    string? FailureReason);
