namespace LinguaCoach.Application.ResourceImport;

// ── Phase H1 — Unified Resource Bank admin read model. ──────────────────────────────────────
//
// A read-only aggregation over the four typed published bank tables
// (CefrVocabularyEntry/CefrGrammarProfileEntry/CefrReadingReference/CefrReadingPassage) so the
// admin can see "one Resource Bank with typed rows and filters" instead of four separate pages.
// This is Option B from docs/architecture/product-model-realignment-h0.md §4: an admin-facing
// read model over the existing typed tables, NOT a physical unified ResourceBankItem table.
// Nothing here writes to any bank table — mutation still only happens through the Resource
// Candidates approve/reject/publish workflow (Phase E4). The existing per-type
// ResourceBankListFilter/List*Async methods above are unchanged and still the source of truth
// for the four typed admin pages; this is an additive aggregation layer on top of them.

public enum UnifiedResourceBankItemType
{
    Vocabulary,
    Grammar,
    ReadingReference,
    ReadingPassage,
    // Phase J5a
    Writing,
    // Phase J5c
    Listening,
    // Phase J5d
    Speaking
}

public sealed record UnifiedResourceBankListFilter(
    UnifiedResourceBankItemType? Type = null,
    string? CefrLevel = null,
    string? Skill = null,
    string? Subskill = null,
    string? ContextTag = null,
    string? FocusTag = null,
    int? DifficultyBand = null,
    string? SearchText = null,
    Guid? SourceId = null,
    int Page = 1,
    int PageSize = 20
);

/// <summary>One row of the unified Resource Bank view. <see cref="SourceTable"/> and
/// <see cref="DetailRoute"/> tell an admin (or a future caller) exactly which typed table/page the
/// row actually lives in — there is no physical unified row behind this DTO, it is assembled on
/// every request from the four typed tables. <see cref="LinkedLearnCount"/>/
/// <see cref="LinkedActivityCount"/>/<see cref="LinkedModuleCount"/> are always null in H1 — Learn
/// Item/Activity/Module do not exist yet (H3/H4/H5); the fields exist now so H3-H5 can populate
/// them without another DTO-shape change.</summary>
public sealed record UnifiedResourceBankItemDto(
    Guid Id,
    UnifiedResourceBankItemType Type,
    string Title,
    string? Summary,
    string CefrLevel,
    string? Skill,
    string? Subskill,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    int? DifficultyBand,
    Guid? SourceId,
    string? SourceName,
    string? ContentFingerprint,
    string? Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string SourceTable,
    string? DetailRoute,
    int? LinkedLearnCount,
    int? LinkedActivityCount,
    int? LinkedModuleCount
);

public sealed record UnifiedResourceBankListResult(
    IReadOnlyList<UnifiedResourceBankItemDto> Items,
    int TotalCount
);
