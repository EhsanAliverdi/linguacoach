namespace LinguaCoach.Application.ResourceImport;

// ── Phase K1 — AI-assisted import column-mapping detection. Mirrors ResourceCandidateAnalysisService's
// existing "AI proposes, deterministic system decides" split (Phase E2): the AI only ever proposes a
// column-rename mapping over a bounded header+sample-row preview; nothing about the actual import
// pipeline (Gate 1-3, InferCandidateType, ExtractCanonicalTextForType, publish/preview) changes.
// An admin always reviews/confirms the proposal before it's applied — see the always-shown review
// step in the Content Import UI. On any AI failure this degrades gracefully to "no suggestion",
// never blocking the underlying import. ──

/// <summary>The full set of column names <see cref="Infrastructure.ResourceImport.ResourceImportService"/>
/// (via Infrastructure) actually recognizes, exposed here so the AI prompt and the admin review UI
/// both stay in sync with the one real source of truth instead of a second hardcoded list drifting
/// out of date.</summary>
public static class ResourceImportRecognizedFields
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "word", "lemma", "headword", "grammarkey", "explanation", "passage", "text", "title",
        "prompt", "transcript", "scenario", "formio", "schema", "template",
        "cefrlevel", "cefr", "skill", "subskill", "tags", "focustags", "difficultyband",
    };
}

public sealed record ResourceImportColumnMappingRequest(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> SampleRows);

/// <summary>One proposed rename: <see cref="SourceColumn"/> is the column name as it appears in the
/// uploaded/pasted file; <see cref="SuggestedField"/> is the recognized field name the AI believes
/// it corresponds to, or null when it found no confident match (left as-is).</summary>
public sealed record ColumnMappingSuggestion(string SourceColumn, string? SuggestedField, double? Confidence);

public sealed record ResourceImportColumnMappingResult(
    bool Success,
    IReadOnlyList<ColumnMappingSuggestion> Suggestions,
    string? ErrorMessage);

public interface IResourceImportColumnMappingService
{
    /// <summary>Never throws — an AI failure (unavailable, bad JSON) returns
    /// <c>Success=false</c> with an empty suggestion list, which the caller treats as "no AI
    /// suggestion available," not an error to surface as a failed import.</summary>
    Task<ResourceImportColumnMappingResult> ProposeMappingAsync(
        ResourceImportColumnMappingRequest request, CancellationToken ct = default);
}
