using LinguaCoach.Application.AdminRepair;

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
    int? LinkedModuleCount,
    bool IsArchived
);

public sealed record UnifiedResourceBankListResult(
    IReadOnlyList<UnifiedResourceBankItemDto> Items,
    int TotalCount
);

// ── Phase K3 — admin archive/unarchive (soft-delete). Single-item and bulk variants both funnel
// through the same handler; bulk is continue-on-error per id, mirroring the Resource Candidates
// batch-action convention (see IResourceCandidateBatchActionService). ──

public sealed record ArchiveResourceBankItemsCommand(IReadOnlyList<Guid> Ids);
public sealed record UnarchiveResourceBankItemsCommand(IReadOnlyList<Guid> Ids);

public sealed record ResourceBankArchiveItemResult(Guid Id, bool Success, string? Error);

public sealed record ResourceBankArchiveResult(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<ResourceBankArchiveItemResult> Items);

public interface IResourceBankArchiveHandler
{
    Task<ResourceBankArchiveResult> ArchiveAsync(ArchiveResourceBankItemsCommand command, CancellationToken ct = default);
    Task<ResourceBankArchiveResult> UnarchiveAsync(UnarchiveResourceBankItemsCommand command, CancellationToken ct = default);
}

// ── Phase K8 — "diagnose then AI-repair" for a single Resource Bank item, e.g. a Vocabulary
// item missing its definition (which otherwise silently blocks multiple-choice Exercise
// generation). See AdminRepairFieldGenerator's doc comment for the shared mechanism. ──

public sealed record ResourceBankItemRepairResult(
    UnifiedResourceBankItemDto Item,
    IReadOnlyList<DiagnosticIssue> IssuesFixed,
    IReadOnlyList<DiagnosticIssue> IssuesRemaining,
    string? ProviderName,
    string? ModelName);

public interface IResourceBankRepairService
{
    Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default);
    Task<ResourceBankItemRepairResult> RepairAsync(Guid id, CancellationToken ct = default);
    Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default);
    Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default);
}

// ── Phase K5 — admin edit of a published Resource Bank item's content/metadata (e.g. correcting
// an AI/import-generated word or CEFR level after the fact). Full-replace semantics per item Type
// — only the fields relevant to the item's own Type are read; the caller (admin UI) is expected to
// have loaded the current values first (same convention as Lesson/Exercise/Module PUT). ──

public sealed record UpdateResourceBankItemCommand(
    Guid Id,
    string CefrLevel,
    string? Subskill,
    int? DifficultyBand,
    IReadOnlyList<string>? ContextTags,
    IReadOnlyList<string>? FocusTags,
    // Vocabulary
    string? Word = null,
    string? PartOfSpeech = null,
    string? Notes = null,
    // Grammar
    string? GrammarPoint = null,
    string? Description = null,
    // ReadingReference
    string? TextType = null,
    string? DifficultyNotes = null,
    string? ReferenceExcerpt = null,
    // ReadingPassage / Writing / Listening / Speaking share Title
    string? Title = null,
    // ReadingPassage
    string? PassageText = null,
    string? Summary = null,
    // Writing / Speaking share PromptText
    string? PromptText = null,
    // Writing
    string? Genre = null,
    int? SuggestedMinWords = null,
    // Listening
    string? Transcript = null,
    // Speaking
    int? SuggestedDurationSeconds = null,
    // Speaking (Phase K20 — describe_image)
    string? ImageUrl = null
);

public interface IResourceBankItemUpdateHandler
{
    Task<UnifiedResourceBankItemDto> HandleAsync(UpdateResourceBankItemCommand command, CancellationToken ct = default);
}

/// <summary>Phase K5 — the full, untruncated, type-specific field set for editing (the list/detail
/// <see cref="UnifiedResourceBankItemDto"/> only carries a flattened, sometimes-truncated Title/
/// Summary for display — not enough to safely round-trip an edit). Only the fields relevant to
/// <see cref="Type"/> are populated; the rest are null.</summary>
public sealed record ResourceBankItemEditDto(
    Guid Id,
    UnifiedResourceBankItemType Type,
    string CefrLevel,
    string? Subskill,
    int? DifficultyBand,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    string? Word,
    string? PartOfSpeech,
    string? Notes,
    string? GrammarPoint,
    string? Description,
    string? TextType,
    string? DifficultyNotes,
    string? ReferenceExcerpt,
    string? Title,
    string? PassageText,
    string? Summary,
    string? PromptText,
    string? Genre,
    int? SuggestedMinWords,
    string? Transcript,
    int? SuggestedDurationSeconds,
    string? ImageUrl
);
