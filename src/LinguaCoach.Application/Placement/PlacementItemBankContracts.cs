namespace LinguaCoach.Application.Placement;

// ── Admin-facing placement item DTO — Form.io-native authoring. FormIoSchemaJson is
// student-safe; ScoringRulesJson is backend-only and never appears in any student-facing DTO. ──

public sealed record AdminPlacementItemDto(
    Guid ItemId,
    string Skill,
    string CefrLevel,
    int ItemOrder,
    bool IsEnabled,
    string? FormIoSchemaJson,
    string? ScoringRulesJson,
    int ScoringRulesVersion,
    string RendererKind = "FormIo",
    /// <summary>Read-only preview of the schema's first component label, for the admin list only
    /// — never persisted, always derived fresh from FormIoSchemaJson.</summary>
    string QuestionPreview = ""
);

// ── List items (server-side paged, optionally filtered by skill) ──────────────

public sealed record ListAdminPlacementItemsQuery(int Page = 1, int PageSize = 20, string? Skill = null, string? Search = null);

/// <summary>Items is the current page only. TotalCount reflects the Skill filter (drives
/// pagination); OverallTotalCount/EnabledCount/SkillCount are always unfiltered, global bank
/// stats for the admin list's KPI strip.</summary>
public sealed record AdminPlacementItemListResult(
    IReadOnlyList<AdminPlacementItemDto> Items,
    int TotalCount,
    int OverallTotalCount,
    int EnabledCount,
    int SkillCount
);

public interface IAdminPlacementItemListQuery
{
    Task<AdminPlacementItemListResult> HandleAsync(ListAdminPlacementItemsQuery query, CancellationToken ct = default);
}

// ── Get a single item ──────────────────────────────────────────────────────────

public sealed record GetAdminPlacementItemQuery(Guid ItemId);

public interface IAdminPlacementItemGetQuery
{
    Task<AdminPlacementItemDto?> HandleAsync(GetAdminPlacementItemQuery query, CancellationToken ct = default);
}

// ── Add item ───────────────────────────────────────────────────────────────────

public sealed record AddPlacementItemCommand(
    string Skill,
    string CefrLevel,
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
