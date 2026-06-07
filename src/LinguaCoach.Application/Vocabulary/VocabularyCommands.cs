using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Vocabulary;

// ── Extraction ────────────────────────────────────────────────────────────────

public sealed record ExtractVocabularyCommand(
    Guid UserId,
    Guid ActivityAttemptId,
    Guid ActivityId,
    Guid? ModuleId,
    string SubmittedContent,
    string FeedbackJson,
    string? ImprovedVersion,
    string? CorrelationId);

public interface IVocabularyExtractionService
{
    /// <summary>
    /// Best-effort extraction. Must not throw — any exception is swallowed internally.
    /// </summary>
    Task ExtractAsync(ExtractVocabularyCommand command, CancellationToken ct = default);
}

// ── Queries ───────────────────────────────────────────────────────────────────

public sealed record GetVocabularyQuery(
    Guid UserId,
    string? Status = null,
    string? Category = null);

public interface IGetVocabularyHandler
{
    Task<IReadOnlyList<StudentVocabularyItemDto>> HandleAsync(GetVocabularyQuery query, CancellationToken ct = default);
}

// ── Status update ─────────────────────────────────────────────────────────────

public sealed record UpdateVocabularyStatusCommand(
    Guid UserId,
    Guid ItemId,
    string Status);

public interface IUpdateVocabularyStatusHandler
{
    Task HandleAsync(UpdateVocabularyStatusCommand command, CancellationToken ct = default);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record StudentVocabularyItemDto(
    Guid Id,
    string Term,
    string? SuggestedPhrase,
    string MeaningOrExplanation,
    string? ExampleSentence,
    string Category,
    string Status,
    string Source,
    int SeenCount,
    DateTime? LastSeenAtUtc,
    DateTime? NextReviewAtUtc,
    DateTime CreatedAt,
    string? SourceActivityTitle,
    string? SourceModuleTitle);
