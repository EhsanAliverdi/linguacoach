using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ResourceImport;

// ── Phase E1 — Import runs / raw records / candidates: admin-facing read contracts +
// the import service contract. No publish/approve workflow — staging only. ──

public sealed record AdminResourceImportRunDto(
    Guid RunId,
    Guid CefrResourceSourceId,
    string SourceName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Status,
    Guid? ImportedByUserId,
    string ImportMode,
    string FileName,
    string FileHash,
    string? SourceVersion,
    string ParserVersion,
    string? AiModelUsed,
    int TotalRecordCount,
    int SucceededCount,
    int RejectedCount,
    int WarningCount,
    string? ErrorSummary,
    string? Notes
);

public sealed record ListAdminResourceImportRunsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? SourceId = null,
    string? Status = null
);

public sealed record AdminResourceImportRunListResult(
    IReadOnlyList<AdminResourceImportRunDto> Items,
    int TotalCount,
    int OverallTotalCount
);

public interface IAdminResourceImportRunListQuery
{
    Task<AdminResourceImportRunListResult> HandleAsync(ListAdminResourceImportRunsQuery query, CancellationToken ct = default);
}

public sealed record GetAdminResourceImportRunQuery(Guid RunId);

public interface IAdminResourceImportRunGetQuery
{
    Task<AdminResourceImportRunDto?> HandleAsync(GetAdminResourceImportRunQuery query, CancellationToken ct = default);
}

// ── Raw records ─────────────────────────────────────────────────────────────────

public sealed record AdminResourceRawRecordDto(
    Guid RawRecordId,
    Guid ResourceImportRunId,
    string? ExternalRecordId,
    string? RawJson,
    string? RawText,
    string RawHash,
    string DetectedLanguageCode,
    string DetectedFormat,
    string ExtractionStatus,
    string? ExtractionWarningsJson,
    DateTime CreatedAt
);

public sealed record ListAdminResourceRawRecordsQuery(
    Guid ResourceImportRunId,
    int Page = 1,
    int PageSize = 50,
    string? ExtractionStatus = null
);

public sealed record AdminResourceRawRecordListResult(
    IReadOnlyList<AdminResourceRawRecordDto> Items,
    int TotalCount
);

public interface IAdminResourceRawRecordListQuery
{
    Task<AdminResourceRawRecordListResult> HandleAsync(ListAdminResourceRawRecordsQuery query, CancellationToken ct = default);
}

public sealed record GetAdminResourceRawRecordQuery(Guid RawRecordId);

public interface IAdminResourceRawRecordGetQuery
{
    Task<AdminResourceRawRecordDto?> HandleAsync(GetAdminResourceRawRecordQuery query, CancellationToken ct = default);
}

// ── Candidates ──────────────────────────────────────────────────────────────────

public sealed record AdminResourceCandidateDto(
    Guid CandidateId,
    Guid ResourceRawRecordId,
    Guid ResourceImportRunId,
    Guid CefrResourceSourceId,
    string CandidateType,
    string CanonicalText,
    string NormalizedJson,
    string LanguageCode,
    string? CefrLevel,
    double? CefrConfidence,
    string? PrimarySkill,
    string? Subskill,
    int? DifficultyBand,
    string? ContextTagsJson,
    string? FocusTagsJson,
    string? GrammarTagsJson,
    string? VocabularyTagsJson,
    string? PronunciationTagsJson,
    string? ActivitySuitabilityTagsJson,
    string? SafetyTagsJson,
    string? LicenseTagsJson,
    double? QualityScore,
    string ContentFingerprint,
    // Phase E2 — AI's raw advisory analysis output (null until IResourceCandidateAnalysisService
    // has analyzed this candidate at least once).
    string? AiAnalysisJson,
    string ValidationStatus,
    string ReviewStatus,
    // Phase E2 broadens this field's meaning — see ResourceCandidate.RejectReason's doc comment:
    // holds the most recent deterministic validation run's {"errors":[...],"warnings":[...]}
    // summary, not just a plain rejection reason.
    string? RejectReason,
    string? AdminNotes,
    DateTime CreatedAt,
    DateTime UpdatedAtUtc,
    // Phase E4 — publish state.
    bool IsPublished,
    DateTimeOffset? PublishedAtUtc,
    string? PublishedEntityType,
    Guid? PublishedEntityId,
    Guid? PublishedByUserId,
    /// <summary>
    /// Client-facing summary of whether Approve &amp; Publish can even be attempted, derived from
    /// ValidationStatus alone (Passed/NeedsReview = attemptable, Failed/Pending = hard-blocked —
    /// see ResourceCandidatePublishService's gate). This is advisory, not authoritative: other live
    /// gates (source approval/license, entity-mapping fields) are only ever re-checked by the
    /// actual publish call, so a "true" here can still fail server-side with a specific error.
    /// </summary>
    bool CanAttemptPublish,
    /// <summary>Non-null only when <see cref="CanAttemptPublish"/> is false and the candidate isn't
    /// already published — the specific hard-error reason(s) from the last validation run.</summary>
    string? PublishBlockReason
);

public sealed record ListAdminResourceCandidatesQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? SourceId = null,
    Guid? ImportRunId = null,
    string? CandidateType = null,
    string? ValidationStatus = null,
    string? ReviewStatus = null,
    string? LanguageCode = null,
    string? CefrLevel = null,
    string? Search = null,
    /// <summary>Phase K2 — when true, restricts to not-yet-published candidates whose
    /// ValidationStatus is Passed or NeedsReview (i.e. CanAttemptPublish — see
    /// AdminResourceCandidateDto), independent of whatever ValidationStatus filter is also
    /// set. Powers the Import Content page's "select all publishable" batch action, which needs
    /// the two-value OR that a single ValidationStatus filter can't express on its own.</summary>
    bool? PublishableOnly = null
);

public sealed record AdminResourceCandidateListResult(
    IReadOnlyList<AdminResourceCandidateDto> Items,
    int TotalCount,
    int OverallTotalCount
);

public interface IAdminResourceCandidateListQuery
{
    Task<AdminResourceCandidateListResult> HandleAsync(ListAdminResourceCandidatesQuery query, CancellationToken ct = default);
}

public sealed record GetAdminResourceCandidateQuery(Guid CandidateId);

public interface IAdminResourceCandidateGetQuery
{
    Task<AdminResourceCandidateDto?> HandleAsync(GetAdminResourceCandidateQuery query, CancellationToken ct = default);
}

/// <summary>
/// Phase K2 — review-state summary for the Import Content page's candidate table, scoped to one
/// import run (or one source, or globally when both are null). Distinguishes "warning-only,
/// still publishable" (NeedsReview) from "hard-blocked" (Failed/Pending) so the UI's headline
/// counts never conflate the two — see the review/publish UX fix's semantics doc.
/// </summary>
public sealed record AdminResourceCandidateReviewSummaryDto(
    int TotalCount,
    int PublishedCount,
    int PassedCount,
    int NeedsReviewCount,
    int BlockedCount,
    int PublishableCount,
    /// <summary>Phase 3 — admin-rejected, regardless of ValidationStatus.</summary>
    int RejectedCount = 0,
    /// <summary>Phase 3 — admin-skipped ("intentionally ignored"), regardless of ValidationStatus.</summary>
    int SkippedCount = 0,
    /// <summary>Phase 3 — awaiting an explicit admin decision (Approve/Reject/Skip), not yet published.</summary>
    int PendingReviewCount = 0
);

public sealed record GetAdminResourceCandidateReviewSummaryQuery(Guid? ImportRunId = null, Guid? SourceId = null);

public interface IAdminResourceCandidateReviewSummaryQuery
{
    Task<AdminResourceCandidateReviewSummaryDto> HandleAsync(
        GetAdminResourceCandidateReviewSummaryQuery query, CancellationToken ct = default);
}

public sealed record SetResourceCandidateAdminNotesCommand(Guid CandidateId, string? AdminNotes);

public interface IAdminResourceCandidateNotesHandler
{
    Task<AdminResourceCandidateDto> HandleAsync(SetResourceCandidateAdminNotesCommand command, CancellationToken ct = default);
}

// ── Phase E4 — admin approve/reject workflow (distinct from ValidationStatus, which stays the
// sole responsibility of IResourceCandidateValidationService). Neither of these ever writes to a
// published Cefr* bank table — that is IResourceCandidatePublishService's job alone. ──

public sealed record ApproveResourceCandidateCommand(Guid CandidateId, string? Notes = null);

public interface IAdminResourceCandidateApproveHandler
{
    Task<AdminResourceCandidateDto> HandleAsync(ApproveResourceCandidateCommand command, CancellationToken ct = default);
}

public sealed record RejectResourceCandidateCommand(Guid CandidateId, string Reason);

public interface IAdminResourceCandidateRejectHandler
{
    Task<AdminResourceCandidateDto> HandleAsync(RejectResourceCandidateCommand command, CancellationToken ct = default);
}

/// <summary>Phase 3 (2026-07-15 import candidate review workflow) — "I am intentionally ignoring
/// this candidate," distinct from PendingReview (never reviewed). See
/// <see cref="ResourceCandidateReviewStatus.Skipped"/>'s doc comment.</summary>
public sealed record SkipResourceCandidateCommand(Guid CandidateId, string? Reason = null);

public interface IAdminResourceCandidateSkipHandler
{
    Task<AdminResourceCandidateDto> HandleAsync(SkipResourceCandidateCommand command, CancellationToken ct = default);
}

/// <summary>
/// Phase 3 (2026-07-15 import candidate review workflow) — edits a staged candidate's content
/// before approval. Every field is independent/optional (null = leave unchanged, matching
/// <see cref="Entities.ResourceCandidate.UpdateContent"/>'s semantics — see that method's doc
/// comment). <paramref name="NormalizedJson"/> is where every type-specific field lives
/// (word/definition/title/body/examples/etc, per <see cref="ResourceCandidateType"/>) since
/// candidates have no per-type typed columns. Re-runs deterministic validation after the edit so
/// <see cref="AdminResourceCandidateDto.ValidationStatus"/>/<see cref="AdminResourceCandidateDto.CanAttemptPublish"/>
/// reflect the edited content immediately, not stale pre-edit gates. Rejected for an
/// already-published candidate — edit through the Resource Bank instead.
/// </summary>
public sealed record UpdateResourceCandidateContentCommand(
    Guid CandidateId,
    string? CanonicalText = null,
    string? NormalizedJson = null,
    string? CefrLevel = null,
    string? PrimarySkill = null,
    string? Subskill = null,
    int? DifficultyBand = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<string>? FocusTags = null
);

public interface IAdminResourceCandidateContentUpdateHandler
{
    Task<AdminResourceCandidateDto> HandleAsync(UpdateResourceCandidateContentCommand command, CancellationToken ct = default);
}

// ── Import service (the parser/gate pipeline) ──────────────────────────────────

// ── Phase H2 — optional row-metadata defaults + forced candidate type, applied by
// ContentImportService (see ContentImportContracts.cs) so an admin-chosen "broad resource
// type" and default metadata fill in whatever a row doesn't already carry itself. A row's own
// value (e.g. its own cefrLevel column) always wins over these defaults. Untouched (all null)
// for every existing file-upload caller — behavior there is unchanged. ──
public sealed record ResourceImportRequest(
    Guid SourceId,
    Stream FileStream,
    string FileName,
    ResourceImportMode ImportMode,
    Guid? ImportedByUserId = null,
    string? Notes = null,
    ResourceCandidateType? DefaultCandidateType = null,
    string? DefaultCefrLevel = null,
    string? DefaultSkill = null,
    string? DefaultSubskill = null,
    IReadOnlyList<string>? DefaultContextTags = null,
    IReadOnlyList<string>? DefaultFocusTags = null,
    int? DefaultDifficultyBand = null,
    /// <summary>Phase K1 — an admin-confirmed column rename map (source column name, case-
    /// insensitive → recognized field name), applied as a pure header rewrite on every parsed row
    /// before any gate runs. Never AI-applied automatically — this is only ever populated from an
    /// admin's confirmed choice in the mapping-review UI. Null/empty means "no renames," the exact
    /// pre-K1 behavior.</summary>
    IReadOnlyDictionary<string, string>? ColumnRenames = null,
    /// <summary>Phase 4 (2026-07-15) — set when this run is one of a package-driven job's
    /// per-file-group runs, so the run is traceable back to its <c>ImportPackage</c>. Null for
    /// every pre-Phase-4 single-file admin upload — behavior there is unchanged.</summary>
    Guid? ImportPackageId = null
);

public sealed record ResourceImportResult(
    Guid RunId,
    string Status,
    int TotalRecordCount,
    int SucceededCount,
    int RejectedCount,
    int WarningCount,
    string? ErrorSummary
);

public interface IResourceImportService
{
    Task<ResourceImportResult> ImportAsync(ResourceImportRequest request, CancellationToken ct = default);

    /// <summary>Phase K1 — parses just the header + a bounded sample of rows (no staging, no DB
    /// writes), used by the AI column-mapping "propose" endpoints.</summary>
    (IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> SampleRows) ParseSample(
        string fileText, ResourceImportMode mode, int sampleSize = 5);
}

public sealed class ResourceImportValidationException : Exception
{
    public ResourceImportValidationException(string message) : base(message) { }
}
