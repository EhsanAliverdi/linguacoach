namespace LinguaCoach.Application.Writing;

/// <summary>
/// Admin read-only query over a student's writing evaluations.
/// Returns activity context and evaluation results. Never exposes raw provider payloads.
/// Phase 17B adds quality summary and per-evaluation dry-run signal preview (never applied to mastery).
/// </summary>
public interface IAdminWritingEvaluationQuery
{
    Task<IReadOnlyList<AdminWritingEvaluationItemDto>> GetForStudentAsync(
        Guid studentProfileId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns pipeline-wide quality metrics and dry-run signal counts.
    /// Dry-run signals are never applied to mastery, CEFR, or Learning Plan progress.
    /// </summary>
    Task<WritingEvaluationQualitySummaryDto> GetQualitySummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a single writing evaluation combined with its computed dry-run signal.
    /// Dry-run signal is never applied to mastery, CEFR, or Learning Plan progress.
    /// </summary>
    Task<WritingEvaluationWithDryRunDto?> GetWithDryRunAsync(Guid evaluationId, CancellationToken ct = default);
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
