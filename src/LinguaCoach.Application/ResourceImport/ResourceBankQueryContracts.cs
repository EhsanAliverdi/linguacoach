namespace LinguaCoach.Application.ResourceImport;

// ── Phase E5 — Published bank browsing/search/admin management. Read-only: nothing here ever
// mutates a Cefr* bank row or a ResourceCandidate. All mutation (approve/reject/publish) still
// lives solely in the Resource Candidates workflow (Phase E4). ──
//
// None of CefrVocabularyEntry/CefrGrammarProfileEntry/CefrReadingReference carries a forward
// reference back to the ResourceCandidate that produced it — the only link is the reverse
// direction (ResourceCandidate.PublishedEntityType/PublishedEntityId, set by
// ResourceCandidatePublishService.MarkPublished). ResourceBankTraceabilityDto below is built by
// querying ResourceCandidate for a row whose PublishedEntityType/PublishedEntityId match the bank
// row being viewed. TraceabilityAvailable is false (with every other field null) when no such
// candidate is found — e.g. a bank row seeded some other way than through the publish workflow.

public sealed record ResourceBankListFilter(
    string? SearchText = null,
    string? CefrLevel = null,
    Guid? SourceId = null,
    int Page = 1,
    int PageSize = 20,
    // Phase E9 — optional selection-metadata filters for the lean bank tables (vocabulary/grammar/
    // reading-references). Null means "no filter on this field". A row with no metadata (a pre-E9
    // or not-yet-backfilled row) simply never matches a metadata filter — it is not treated as a
    // wildcard match.
    string? ContextTag = null,
    string? FocusTag = null,
    string? Subskill = null,
    int? DifficultyBand = null
);

/// <summary>Reverse-lookup traceability back to the originating ResourceCandidate/ImportRun, or a
/// clear "unavailable" state when no matching candidate exists.</summary>
public sealed record ResourceBankTraceabilityDto(
    bool TraceabilityAvailable,
    Guid? CandidateId,
    Guid? ResourceImportRunId,
    string? ContentFingerprint,
    double? QualityScore,
    DateTime? CandidateCreatedAt,
    DateTimeOffset? PublishedAtUtc,
    Guid? PublishedByUserId
)
{
    public static readonly ResourceBankTraceabilityDto Unavailable =
        new(false, null, null, null, null, null, null, null);
}

// ── Vocabulary ──────────────────────────────────────────────────────────────────

public sealed record ResourceBankVocabularyListItemDto(
    Guid Id,
    string Word,
    string CefrLevel,
    string? PartOfSpeech,
    string? Notes,
    Guid SourceId,
    string SourceName,
    DateTime CreatedAt,
    // Phase E9 — published selection metadata (read-only).
    string? Subskill = null,
    int? DifficultyBand = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<string>? FocusTags = null
);

public sealed record ResourceBankVocabularyDetailDto(
    Guid Id,
    string Word,
    string CefrLevel,
    string? PartOfSpeech,
    string? Notes,
    DateTime CreatedAt,
    ResourceCandidateSourceInfoDto Source,
    ResourceBankTraceabilityDto Traceability,
    // Phase E9 — published selection metadata (read-only).
    string? Subskill = null,
    int? DifficultyBand = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<string>? FocusTags = null
);

public sealed record ResourceBankVocabularyListResult(
    IReadOnlyList<ResourceBankVocabularyListItemDto> Items,
    int TotalCount
);

// ── Grammar ─────────────────────────────────────────────────────────────────────

public sealed record ResourceBankGrammarListItemDto(
    Guid Id,
    string GrammarPoint,
    string CefrLevel,
    string? Description,
    Guid SourceId,
    string SourceName,
    DateTime CreatedAt,
    // Phase E9 — published selection metadata (read-only).
    string? Subskill = null,
    int? DifficultyBand = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<string>? FocusTags = null
);

public sealed record ResourceBankGrammarDetailDto(
    Guid Id,
    string GrammarPoint,
    string CefrLevel,
    string? Description,
    DateTime CreatedAt,
    ResourceCandidateSourceInfoDto Source,
    ResourceBankTraceabilityDto Traceability,
    // Phase E9 — published selection metadata (read-only).
    string? Subskill = null,
    int? DifficultyBand = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<string>? FocusTags = null
);

public sealed record ResourceBankGrammarListResult(
    IReadOnlyList<ResourceBankGrammarListItemDto> Items,
    int TotalCount
);

// ── Reading references ─────────────────────────────────────────────────────────

public sealed record ResourceBankReadingReferenceListItemDto(
    Guid Id,
    string CefrLevel,
    string? TextType,
    string? DifficultyNotes,
    string? ReferenceExcerpt,
    Guid SourceId,
    string SourceName,
    DateTime CreatedAt,
    // Phase E9 — published selection metadata (read-only).
    string? Subskill = null,
    int? DifficultyBand = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<string>? FocusTags = null
);

public sealed record ResourceBankReadingReferenceDetailDto(
    Guid Id,
    string CefrLevel,
    string? TextType,
    string? DifficultyNotes,
    string? ReferenceExcerpt,
    DateTime CreatedAt,
    ResourceCandidateSourceInfoDto Source,
    ResourceBankTraceabilityDto Traceability,
    // Phase E9 — published selection metadata (read-only).
    string? Subskill = null,
    int? DifficultyBand = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<string>? FocusTags = null
);

public sealed record ResourceBankReadingReferenceListResult(
    IReadOnlyList<ResourceBankReadingReferenceListItemDto> Items,
    int TotalCount
);

// ── Reading passages (Phase E7 — full-length passages, distinct from ReadingReference) ────────

public sealed record ResourceBankReadingPassageListItemDto(
    Guid Id,
    string Title,
    string CefrLevel,
    int WordCount,
    int EstimatedReadingMinutes,
    string? Subskill,
    Guid SourceId,
    string SourceName,
    DateTime CreatedAt
);

public sealed record ResourceBankReadingPassageDetailDto(
    Guid Id,
    string Title,
    string PassageText,
    string? Summary,
    string CefrLevel,
    int? DifficultyBand,
    string PrimarySkill,
    string? Subskill,
    IReadOnlyList<string> TopicTags,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    int WordCount,
    int EstimatedReadingMinutes,
    string? AttributionText,
    double? QualityScore,
    DateTime CreatedAt,
    ResourceCandidateSourceInfoDto Source,
    ResourceBankTraceabilityDto Traceability
);

public sealed record ResourceBankReadingPassageListResult(
    IReadOnlyList<ResourceBankReadingPassageListItemDto> Items,
    int TotalCount
);

// ── Query service ───────────────────────────────────────────────────────────────

public interface IResourceBankQueryService
{
    Task<ResourceBankVocabularyListResult> ListVocabularyAsync(ResourceBankListFilter filter, CancellationToken ct = default);
    Task<ResourceBankVocabularyDetailDto?> GetVocabularyDetailAsync(Guid id, CancellationToken ct = default);

    Task<ResourceBankGrammarListResult> ListGrammarAsync(ResourceBankListFilter filter, CancellationToken ct = default);
    Task<ResourceBankGrammarDetailDto?> GetGrammarDetailAsync(Guid id, CancellationToken ct = default);

    Task<ResourceBankReadingReferenceListResult> ListReadingReferencesAsync(ResourceBankListFilter filter, CancellationToken ct = default);
    Task<ResourceBankReadingReferenceDetailDto?> GetReadingReferenceDetailAsync(Guid id, CancellationToken ct = default);

    Task<ResourceBankReadingPassageListResult> ListReadingPassagesAsync(ResourceBankListFilter filter, CancellationToken ct = default);
    Task<ResourceBankReadingPassageDetailDto?> GetReadingPassageDetailAsync(Guid id, CancellationToken ct = default);

    // Phase H1 — unified read model over all four typed bank tables. See
    // UnifiedResourceBankContracts.cs for the DTO/filter shapes and their rationale.
    Task<UnifiedResourceBankListResult> ListUnifiedAsync(UnifiedResourceBankListFilter filter, CancellationToken ct = default);

    /// <summary>Phase K3 — single-row lookup by <see cref="Domain.Entities.ResourceBankItem.Id"/>,
    /// backing the admin "view as its own page" detail route. Null when no row with that id exists
    /// (including an archived row — archived items are still real rows, just excluded from the
    /// default list, so this still returns them for a direct-by-id lookup).</summary>
    Task<UnifiedResourceBankItemDto?> GetUnifiedByIdAsync(Guid id, CancellationToken ct = default);
}
