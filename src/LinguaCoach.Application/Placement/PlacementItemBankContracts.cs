namespace LinguaCoach.Application.Placement;

// ── Admin-facing placement item DTO — includes CorrectAnswer (admin-only, unlike the
// student-facing PlacementNextItemDto which deliberately excludes it) ────────────────

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
    bool IsEnabled
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
    string ItemType,
    string Prompt,
    string CorrectAnswer,
    string? ReadingPassage,
    string? ListeningAudioScript,
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
    string ItemType,
    string Prompt,
    string CorrectAnswer,
    string? ReadingPassage,
    string? ListeningAudioScript,
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
