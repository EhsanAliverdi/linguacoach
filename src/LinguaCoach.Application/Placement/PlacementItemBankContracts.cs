namespace LinguaCoach.Application.Placement;

// ── Admin-facing placement item DTO — Form.io-native authoring. FormIoSchemaJson is
// student-safe; ScoringRulesJson is backend-only and never appears in any student-facing DTO. ──

public sealed record AdminPlacementItemDto(
    Guid ItemId,
    string Skill,
    string CefrLevel,
    string ItemType,
    string Prompt,
    int ItemOrder,
    bool IsEnabled,
    string? FormIoSchemaJson,
    string? ScoringRulesJson,
    int ScoringRulesVersion,
    string RendererKind = "FormIo"
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
    int ItemOrder,
    bool IsEnabled,
    string FormIoSchemaJson,
    string ScoringRulesJson,
    string RendererKind = "FormIo"
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
    int ItemOrder,
    bool IsEnabled,
    string FormIoSchemaJson,
    string ScoringRulesJson,
    string RendererKind = "FormIo"
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
