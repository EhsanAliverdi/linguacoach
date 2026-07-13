namespace LinguaCoach.Application.ResourceImport;

// ── Phase E3 — Admin rendered preview for a staged ResourceCandidate. Strictly read-only: never
// mutates the candidate (no UpdatedAtUtc bump, no SaveChangesAsync), never writes to any
// published Cefr* bank table, and never decides ValidationStatus/ReviewStatus. This is a
// per-candidate preview projection only — no publish/approve action exists here (Phase E4). ──

/// <summary>Source/provenance/license info surfaced to the admin-only preview panel.</summary>
public sealed record ResourceCandidateSourceInfoDto(
    Guid SourceId,
    string SourceName,
    string LicenseType,
    string? SourceUrl,
    string? DownloadUrl,
    string? AttributionText,
    bool AllowsStudentDisplay,
    bool AllowsCommercialUse);

/// <summary>Parsed tag arrays — each JSON tag column decoded into a plain string array.</summary>
public sealed record ResourceCandidateTagsDto(
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    IReadOnlyList<string> GrammarTags,
    IReadOnlyList<string> VocabularyTags,
    IReadOnlyList<string> PronunciationTags,
    IReadOnlyList<string> ActivitySuitabilityTags);

/// <summary>A few key AI-suggested classification fields — admin-only, advisory (the AI never
/// decides ValidationStatus). See <see cref="ResourceCandidatePreviewDto.AiAnalysisDetailsJson"/>
/// for the raw response this was summarized from.</summary>
public sealed record ResourceCandidateAiAnalysisSummaryDto(
    string? CefrLevel,
    double? CefrConfidence,
    string? PrimarySkill,
    string? Subskill,
    int? DifficultyBand,
    double? QualityScore,
    IReadOnlyList<string> SafetyTags);

public sealed record ResourceCandidateRawRecordSummaryDto(
    Guid RawRecordId,
    string ExtractionStatus,
    string Excerpt);

public sealed record ResourceCandidateImportRunSummaryDto(
    Guid ImportRunId,
    Guid SourceId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Status);

/// <summary>
/// One flexible rendered-preview projection covering all candidate-type shapes
/// (VocabularyEntry/GrammarProfileEntry/ReadingPassage/ActivityTemplateCandidate/WritingPrompt/
/// Unknown) — only the fields relevant to <see cref="Kind"/> are populated. A single flexible shape
/// is simpler than one separate DTO per type for a preview projection that is never persisted.
///
/// <see cref="StudentVisibleFormIoSchemaJson"/> is the ONLY slot ever surfaced to a "what the
/// student would see" panel for an ActivityTemplateCandidate row. Nothing scoring/rubric-shaped
/// is ever placed there — any such metadata found on the row is exposed only via
/// <see cref="ResourceCandidatePreviewDto.AdminOnlyActivityMetadataJson"/>.
/// </summary>
public sealed record ResourceCandidateRenderedPreviewDto(
    string Kind,
    // VocabularyEntry
    string? Word = null,
    string? PartOfSpeech = null,
    string? Definition = null,
    string? Example = null,
    // GrammarProfileEntry
    string? GrammarTitle = null,
    string? Explanation = null,
    IReadOnlyList<string>? GrammarExamples = null,
    // ReadingPassage
    string? Title = null,
    string? PassageText = null,
    int? WordCount = null,
    int? EstimatedReadingMinutes = null,
    // ActivityTemplateCandidate — student-visible slot only, never leaked scoring/rubric data
    string? StudentVisibleFormIoSchemaJson = null,
    // WritingPrompt (Phase J5a)
    string? PromptText = null,
    string? Genre = null,
    int? SuggestedMinWords = null,
    // ListeningPassage (Phase J5c) — Transcript reuses Explanation-shaped free text; HasAudio
    // tells the admin UI whether an audio file has been uploaded yet (never null/empty at
    // publish time — see ResourceCandidatePublishService's audio-required gate).
    string? Transcript = null,
    bool HasAudio = false,
    // SpeakingPrompt (Phase J5d) — reuses PromptText/Title above (same shape as WritingPrompt);
    // this is the only field genuinely new to this type.
    int? SuggestedDurationSeconds = null,
    // Unknown / generic fallback
    IReadOnlyList<string>? FieldSummary = null);

/// <summary>
/// Phase E3 rendered-preview projection for one staged candidate. Read-only — building this
/// never mutates the candidate row or writes to any published Cefr* bank table.
/// </summary>
public sealed record ResourceCandidatePreviewDto(
    Guid CandidateId,
    string CandidateType,
    string Title,
    string LanguageCode,
    string CanonicalText,
    /// <summary>A safe-to-display projection of NormalizedJson (field-name-keyed, values bounded
    /// in length) — not necessarily the raw column verbatim.</summary>
    IReadOnlyDictionary<string, string?> NormalizedContent,
    ResourceCandidateRenderedPreviewDto RenderedPreviewModel,
    ResourceCandidateSourceInfoDto Source,
    string? CefrLevel,
    double? CefrConfidence,
    string? PrimarySkill,
    string? Subskill,
    int? DifficultyBand,
    ResourceCandidateTagsDto Tags,
    double? QualityScore,
    IReadOnlyList<string> SafetyIssues,
    string ValidationStatus,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings,
    string ReviewStatus,
    string ContentFingerprint,
    /// <summary>Re-derived from the validation warnings already stored in RejectReason (matching
    /// ResourceCandidateValidationService's exact "Duplicate:"-prefixed wording) rather than
    /// re-querying the candidate table.</summary>
    IReadOnlyList<string> DuplicateIndicators,
    ResourceCandidateAiAnalysisSummaryDto? AiAnalysisSummary,
    /// <summary>Raw AiAnalysisJson — admin-only, never rendered in a student-visible slot.</summary>
    string? AiAnalysisDetailsJson,
    ResourceCandidateRawRecordSummaryDto RawRecordSummary,
    ResourceCandidateImportRunSummaryDto ImportRunSummary,
    bool CanPreview,
    IReadOnlyList<string> PreviewWarnings,
    /// <summary>Scoring/rubric/answer-key-shaped fields found on an ActivityTemplateCandidate
    /// row's raw data, if any — admin-only, NEVER merged into RenderedPreviewModel's
    /// student-visible slot. Null for every other candidate type or when none were found.</summary>
    string? AdminOnlyActivityMetadataJson);

public interface IResourceCandidatePreviewService
{
    /// <summary>
    /// Builds a read-only rendered preview for one staged candidate. Returns null when the
    /// candidate does not exist (caller maps that to 404 — no exception is thrown for a plain
    /// not-found lookup, matching this controller's other Get-by-id query conventions). Never
    /// throws for an unsupported/malformed candidate shape — that degrades to
    /// <c>CanPreview = false</c> plus a <c>PreviewWarnings</c> entry instead.
    /// </summary>
    Task<ResourceCandidatePreviewDto?> GetPreviewAsync(Guid candidateId, CancellationToken ct = default);
}
