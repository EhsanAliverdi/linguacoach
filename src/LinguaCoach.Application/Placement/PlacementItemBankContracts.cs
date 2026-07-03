using LinguaCoach.Domain.Questions;

namespace LinguaCoach.Application.Placement;

// ── Admin-facing placement item DTO — includes CorrectAnswer (admin-only, unlike the
// student-facing PlacementNextItemDto which deliberately excludes it) ────────────────
//
// The legacy flat fields (ItemType/Prompt/CorrectAnswer/ReadingPassage/ListeningAudioScript)
// are now *derived* from Content (Unified Question-Schema Phase 4) rather than admin-authored
// directly — kept only for display continuity and PlacementAssessmentService's still flat-field-
// driven adaptive algorithm, dropped once that's cut over (Phase 7).

public sealed record AdminPlacementItemDto(
    Guid ItemId,
    string Skill,
    string CefrLevel,
    string ItemType,
    string Prompt,
    string CorrectAnswer,
    string? ReadingPassage,
    string? ListeningAudioScript,
    int ItemOrder,
    bool IsEnabled,
    QuestionContent Content
);

// ── List all items ────────────────────────────────────────────────────────────

public sealed record ListAdminPlacementItemsQuery();

public interface IAdminPlacementItemListQuery
{
    Task<IReadOnlyList<AdminPlacementItemDto>> HandleAsync(ListAdminPlacementItemsQuery query, CancellationToken ct = default);
}

// ── Add item ───────────────────────────────────────────────────────────────────

public sealed record AddPlacementItemCommand(
    string Skill,
    string CefrLevel,
    QuestionContent Content,
    int ItemOrder,
    bool IsEnabled
);

public interface IAdminAddPlacementItemHandler
{
    Task<AdminPlacementItemDto> HandleAsync(AddPlacementItemCommand command, CancellationToken ct = default);
}

// ── Update item ────────────────────────────────────────────────────────────────

public sealed record UpdatePlacementItemCommand(
    Guid ItemId,
    string Skill,
    string CefrLevel,
    QuestionContent Content,
    int ItemOrder,
    bool IsEnabled
);

public interface IAdminUpdatePlacementItemHandler
{
    Task<AdminPlacementItemDto> HandleAsync(UpdatePlacementItemCommand command, CancellationToken ct = default);
}

// ── Remove item ────────────────────────────────────────────────────────────────

public sealed record RemovePlacementItemCommand(Guid ItemId);

public interface IAdminRemovePlacementItemHandler
{
    Task HandleAsync(RemovePlacementItemCommand command, CancellationToken ct = default);
}

// ── Validation error ─────────────────────────────────────────────────────────

public sealed class PlacementItemValidationException : Exception
{
    public PlacementItemValidationException(string message) : base(message) { }
}
