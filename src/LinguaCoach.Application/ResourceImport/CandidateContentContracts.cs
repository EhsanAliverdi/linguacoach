using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ResourceImport;

/// <summary>
/// Phase 4.5 — typed, discriminated candidate content schemas. Replaces ad-hoc field-name lookups
/// scattered across <see cref="Infrastructure.ResourceImport.ResourceCandidateFieldHelper"/>-style
/// callers with one real .NET type per supported <see cref="ResourceCandidateType"/>. A
/// <see cref="Domain.Entities.ResourceCandidate"/> still stores its content as
/// <c>NormalizedJson</c> (no schema-version column was added — see
/// <see cref="IResourceCandidateContentSerializer"/>'s doc comment for why); these records are the
/// typed view over that JSON, produced/consumed only through the central serializer, never parsed
/// ad hoc elsewhere.
///
/// Only the six candidate types that currently have a real publish target are represented here
/// (mirrors <see cref="Infrastructure.ResourceImport.ResourceCandidatePublishService"/>'s supported
/// set) — <see cref="ResourceCandidateType.ActivityTemplateCandidate"/> and
/// <see cref="ResourceCandidateType.Unknown"/> are deliberately not given typed schemas; publishing
/// them is still blocked exactly as before.
/// </summary>
public abstract record CandidateContent;

/// <summary>Reading is one schema covering both the short "reference excerpt" and full-length
/// "passage" bank routing (see <see cref="Infrastructure.ResourceImport.ResourceCandidatePublishService.MaxReadingExcerptLength"/>)
/// — the candidate stage doesn't know yet which one it will become; that routing decision is made
/// at publish time from <see cref="PassageText"/>'s length, same as before this phase.</summary>
public sealed record ReadingCandidateContent(
    string PassageText,
    string? Title = null,
    string? TextType = null,
    string? ReferenceSource = null) : CandidateContent;

public sealed record VocabularyCandidateContent(
    string Word,
    string Definition,
    string? PartOfSpeech = null,
    IReadOnlyList<string>? Examples = null) : CandidateContent;

public sealed record GrammarCandidateContent(
    string Title,
    string Explanation,
    IReadOnlyList<string>? Examples = null,
    IReadOnlyList<string>? CommonMistakes = null) : CandidateContent;

/// <summary>The candidate's uploaded audio file itself is tracked separately on
/// <see cref="Domain.Entities.ResourceCandidate.AudioStorageKey"/>/<c>AudioContentType</c> (see
/// <see cref="Domain.Entities.ResourceCandidate.AttachAudio"/>) — not part of this JSON-backed
/// content, exactly as before this phase.</summary>
public sealed record ListeningCandidateContent(
    string Title,
    string? Transcript = null) : CandidateContent;

public sealed record SpeakingCandidateContent(
    string Title,
    string PromptText,
    string? Instructions = null,
    string? Context = null,
    int? SuggestedDurationSeconds = null) : CandidateContent;

public sealed record WritingCandidateContent(
    string Title,
    string PromptText,
    string? Instructions = null,
    string? Genre = null,
    int? SuggestedMinWords = null,
    string? ExpectedLevel = null) : CandidateContent;

/// <summary>One structured validation failure on a typed candidate content field. FieldName uses
/// the typed schema's own canonical property names (e.g. "word", "definition", "passageText"), not
/// the many legacy source-column aliases a candidate's NormalizedJson may still carry.</summary>
public sealed record CandidateFieldError(string FieldName, string Message);

public sealed record CandidateContentValidationResult(bool IsValid, IReadOnlyList<CandidateFieldError> Errors)
{
    public static CandidateContentValidationResult Valid() => new(true, Array.Empty<CandidateFieldError>());
    public static CandidateContentValidationResult Invalid(IEnumerable<CandidateFieldError> errors) => new(false, errors.ToArray());
}

/// <summary><see cref="WasLegacyMapped"/> is true when the source NormalizedJson used a legacy
/// alias column name (e.g. "lemma" instead of "word", "grammarKey" instead of "title") rather than
/// the typed schema's own canonical field name — informational only, never blocks parsing.</summary>
public sealed record CandidateContentParseResult(
    bool Success,
    CandidateContent? Content,
    IReadOnlyList<CandidateFieldError> Errors,
    bool WasLegacyMapped)
{
    public static CandidateContentParseResult Ok(CandidateContent content, bool wasLegacyMapped) =>
        new(true, content, Array.Empty<CandidateFieldError>(), wasLegacyMapped);

    public static CandidateContentParseResult Failed(params CandidateFieldError[] errors) =>
        new(false, null, errors, false);
}

/// <summary>Thrown by callers (content-update handler, approve handler) that must hard-block on
/// invalid typed content rather than silently persisting/approving it. Carries the same structured
/// <see cref="CandidateFieldError"/> list a caller would get from
/// <see cref="IResourceCandidateContentSerializer.Validate"/>, so an API layer can surface exact
/// per-field messages instead of one flattened string.</summary>
public sealed class CandidateContentValidationException : Exception
{
    public IReadOnlyList<CandidateFieldError> Errors { get; }

    public CandidateContentValidationException(IReadOnlyList<CandidateFieldError> errors)
        : base("Candidate content failed typed schema validation: " +
               string.Join("; ", errors.Select(e => $"{e.FieldName}: {e.Message}")))
    {
        Errors = errors;
    }
}

/// <summary>
/// Phase 4.5 — the single place that parses, validates, and serializes a
/// <see cref="Domain.Entities.ResourceCandidate.NormalizedJson"/> payload into/from a typed
/// <see cref="CandidateContent"/>. No other controller/service should call
/// <c>JsonSerializer</c>/<c>JsonDocument</c> directly on a candidate's NormalizedJson — route
/// through here instead, so there is exactly one field-name mapping table to maintain.
///
/// <b>Migration/compatibility strategy</b> (deliberately the smallest safe design — see the Phase
/// 4.5 review doc for the alternatives considered): no new column was added to
/// <see cref="Domain.Entities.ResourceCandidate"/>. Every candidate type's alias table lists its
/// canonical field name first and every legacy source-column name understood by the pre-4.5
/// field-alias lookups (word/lemma/headword, grammarKey/title, passage/text, prompt, transcript,
/// scenario, durationSeconds, minWords, etc.) after it, so a pre-4.5 row parses through the exact
/// same lookup path a brand-new typed row does — there is no separate, hidden "legacy fallback"
/// code path to silently maintain forever. Content written by <see cref="Serialize"/> always uses
/// the canonical field names, so over time, as candidates are re-saved, storage converges on the
/// canonical shape on its own.
/// </summary>
public interface IResourceCandidateContentSerializer
{
    /// <summary>True for the six candidate types that have a typed schema and a real Resource Bank
    /// publish target (Vocabulary/Grammar/Reading/Listening/Speaking/Writing). False for
    /// ActivityTemplateCandidate and Unknown, which have neither.</summary>
    bool SupportsTypedSchema(ResourceCandidateType candidateType);

    /// <summary>
    /// <paramref name="canonicalTextFallback"/>, when given, fills the type's primary field
    /// (Word/Title/PassageText/PromptText) if the NormalizedJson didn't carry one — this exactly
    /// reproduces the pre-4.5 publish-time fallback ("use CanonicalText if the row has no
    /// word/title/passage/prompt field"), now centralized here instead of duplicated per Build*
    /// method. Pass null (the edit/approve-gate call site does) to validate exactly what was typed,
    /// with no fallback rescuing an empty required field.
    /// </summary>
    CandidateContentParseResult Parse(ResourceCandidateType candidateType, string normalizedJson, string? canonicalTextFallback = null);

    CandidateContentValidationResult Validate(ResourceCandidateType candidateType, CandidateContent content);

    /// <summary>Serializes a typed content instance back into the canonical-field-name JSON stored
    /// in <see cref="Domain.Entities.ResourceCandidate.NormalizedJson"/>.</summary>
    string Serialize(CandidateContent content);
}
