using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ResourceImport;

// ── Phase H2 — Import Content UX v1. A product-friendly wrapper around the existing Phase E1
// ImportAsync pipeline (see IResourceImportService): admin pastes text/CSV/JSON, picks a broad
// resource type + default metadata, and this service finds-or-creates the named
// CefrResourceSource, converts the pasted content into the shape ImportAsync already accepts,
// and applies the admin's choices as row-metadata defaults (see ResourceImportRequest's Phase
// H2 fields). Never writes to a published Cefr* bank table itself — everything it creates stays
// a pending ResourceCandidate, exactly like a file-upload import. No AI structuring: mapping is
// either a row's own columns or the admin's explicit defaults, nothing is guessed. ──

/// <summary>Phase H2 input mode. Deliberately textual only — <c>PastedText</c> is line-based (one
/// item per non-empty line), <c>CsvText</c>/<c>JsonText</c> reuse the existing CSV/JSON parsers
/// verbatim. File upload already exists as its own flow (see <c>AdminResourceImportController</c>)
/// and is out of scope here — see docs/architecture/product-model-realignment-h0.md.</summary>
public enum ContentImportInputMode
{
    PastedText = 0,
    CsvText = 1,
    JsonText = 2
}

public sealed record ContentImportRequest(
    string SourceName,
    /// <summary>Phase J5b — null means "Mixed": don't force a type, let
    /// <see cref="IResourceImportService"/>'s existing per-row field-name inference
    /// (<c>InferCandidateType</c>) classify each row independently.</summary>
    ResourceCandidateType? ResourceType,
    ContentImportInputMode InputMode,
    string Content,
    string? DefaultCefrLevel = null,
    string? DefaultSkill = null,
    string? DefaultSubskill = null,
    IReadOnlyList<string>? DefaultContextTags = null,
    IReadOnlyList<string>? DefaultFocusTags = null,
    int? DefaultDifficultyBand = null,
    string? Notes = null,
    Guid? ImportedByUserId = null,
    /// <summary>Phase K1 — an admin-confirmed column rename map, forwarded verbatim to
    /// <see cref="ResourceImportRequest.ColumnRenames"/>.</summary>
    IReadOnlyDictionary<string, string>? ColumnRenames = null
);

public sealed record ContentImportResult(
    Guid ImportRunId,
    Guid SourceId,
    int RawRecordCount,
    int CandidateCount,
    int WarningCount,
    string Status,
    string? ErrorSummary,
    string ReviewRoute
);

public interface IContentImportService
{
    Task<ContentImportResult> ImportContentAsync(ContentImportRequest request, CancellationToken ct = default);

    /// <summary>Phase K1 — converts pasted content into the same (fileText, mode) shape
    /// <see cref="ImportContentAsync"/> uses internally, for the AI column-mapping "propose"
    /// endpoint. Pure conversion, no DB access.</summary>
    (string FileText, ResourceImportMode Mode) ConvertForPreview(ContentImportInputMode inputMode, string content);
}
