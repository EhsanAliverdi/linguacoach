using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.5 — real implementation of <see cref="IResourceCandidateContentSerializer"/>. Reuses
/// <see cref="ResourceCandidateFieldHelper"/>'s exact field-name-keyed JSON parsing so a candidate
/// staged before this phase and one staged after it go through the identical lookup path — see the
/// interface's doc comment for the full migration/compatibility reasoning.
/// </summary>
public sealed class ResourceCandidateContentSerializer : IResourceCandidateContentSerializer
{
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.Web);

    public bool SupportsTypedSchema(ResourceCandidateType candidateType) => candidateType switch
    {
        ResourceCandidateType.VocabularyEntry => true,
        ResourceCandidateType.GrammarProfileEntry => true,
        ResourceCandidateType.ReadingPassage => true,
        ResourceCandidateType.ListeningPassage => true,
        ResourceCandidateType.SpeakingPrompt => true,
        ResourceCandidateType.WritingPrompt => true,
        _ => false,
    };

    public CandidateContentParseResult Parse(ResourceCandidateType candidateType, string normalizedJson, string? canonicalTextFallback = null)
    {
        if (!SupportsTypedSchema(candidateType))
        {
            return CandidateContentParseResult.Failed(
                new CandidateFieldError("candidateType", $"CandidateType '{candidateType}' has no typed schema."));
        }

        var fields = ResourceCandidateFieldHelper.ParseFields(normalizedJson);
        if (fields.Count == 0)
        {
            return CandidateContentParseResult.Failed(
                new CandidateFieldError("normalizedJson", "Content is empty or is not a valid JSON object."));
        }

        return candidateType switch
        {
            ResourceCandidateType.VocabularyEntry => ParseVocabulary(fields, canonicalTextFallback),
            ResourceCandidateType.GrammarProfileEntry => ParseGrammar(fields, canonicalTextFallback),
            ResourceCandidateType.ReadingPassage => ParseReading(fields, canonicalTextFallback),
            ResourceCandidateType.ListeningPassage => ParseListening(fields, canonicalTextFallback),
            ResourceCandidateType.SpeakingPrompt => ParseSpeaking(fields, canonicalTextFallback),
            ResourceCandidateType.WritingPrompt => ParseWriting(fields, canonicalTextFallback),
            _ => CandidateContentParseResult.Failed(new CandidateFieldError("candidateType", "Unsupported.")),
        };
    }

    public CandidateContentValidationResult Validate(ResourceCandidateType candidateType, CandidateContent content)
    {
        var errors = new List<CandidateFieldError>();

        switch (content)
        {
            case VocabularyCandidateContent v:
                if (candidateType != ResourceCandidateType.VocabularyEntry)
                    errors.Add(TypeMismatch(candidateType, "VocabularyEntry"));
                if (string.IsNullOrWhiteSpace(v.Word))
                    errors.Add(new CandidateFieldError("word", "Word is required."));
                // Definition is deliberately not hard-required — VocabularyContent.Notes (the bank
                // entity field it maps to) is nullable, matching pre-4.5 publish behavior where a
                // definition-less row still published successfully.
                break;

            case GrammarCandidateContent g:
                if (candidateType != ResourceCandidateType.GrammarProfileEntry)
                    errors.Add(TypeMismatch(candidateType, "GrammarProfileEntry"));
                if (string.IsNullOrWhiteSpace(g.Title))
                    errors.Add(new CandidateFieldError("title", "Title is required."));
                // Explanation is deliberately not hard-required — GrammarContent.Description (the
                // bank entity field it maps to) is nullable, matching pre-4.5 publish behavior.
                break;

            case ReadingCandidateContent r:
                if (candidateType != ResourceCandidateType.ReadingPassage)
                    errors.Add(TypeMismatch(candidateType, "ReadingPassage"));
                if (string.IsNullOrWhiteSpace(r.PassageText))
                    errors.Add(new CandidateFieldError("passageText", "Passage text is required."));
                break;

            case ListeningCandidateContent l:
                if (candidateType != ResourceCandidateType.ListeningPassage)
                    errors.Add(TypeMismatch(candidateType, "ListeningPassage"));
                if (string.IsNullOrWhiteSpace(l.Title))
                    errors.Add(new CandidateFieldError("title", "Title is required."));
                break;

            case SpeakingCandidateContent s:
                if (candidateType != ResourceCandidateType.SpeakingPrompt)
                    errors.Add(TypeMismatch(candidateType, "SpeakingPrompt"));
                if (string.IsNullOrWhiteSpace(s.Title))
                    errors.Add(new CandidateFieldError("title", "Title is required."));
                if (string.IsNullOrWhiteSpace(s.PromptText))
                    errors.Add(new CandidateFieldError("promptText", "Prompt text is required."));
                if (s.SuggestedDurationSeconds is < 1)
                    errors.Add(new CandidateFieldError("suggestedDurationSeconds", "Suggested duration must be a positive number of seconds."));
                break;

            case WritingCandidateContent w:
                if (candidateType != ResourceCandidateType.WritingPrompt)
                    errors.Add(TypeMismatch(candidateType, "WritingPrompt"));
                if (string.IsNullOrWhiteSpace(w.Title))
                    errors.Add(new CandidateFieldError("title", "Title is required."));
                if (string.IsNullOrWhiteSpace(w.PromptText))
                    errors.Add(new CandidateFieldError("promptText", "Prompt text is required."));
                if (w.SuggestedMinWords is < 1)
                    errors.Add(new CandidateFieldError("suggestedMinWords", "Suggested minimum word count must be positive."));
                break;

            default:
                errors.Add(new CandidateFieldError("content", $"Unrecognized candidate content type '{content.GetType().Name}'."));
                break;
        }

        return errors.Count == 0 ? CandidateContentValidationResult.Valid() : CandidateContentValidationResult.Invalid(errors);
    }

    public string Serialize(CandidateContent content) => JsonSerializer.Serialize(content, content.GetType(), SerializeOptions);

    private static CandidateFieldError TypeMismatch(ResourceCandidateType candidateType, string expected) =>
        new("candidateType", $"Content is a {expected} shape but CandidateType is '{candidateType}'.");

    // ── Per-type field-alias parsing — canonical name listed first, legacy source-column aliases
    // after it. Mirrors the alias lists ResourceCandidatePublishService used directly before this
    // phase, so pre-4.5 candidates parse identically to how they published before. ──────────────

    private static CandidateContentParseResult ParseVocabulary(IReadOnlyDictionary<string, string?> fields, string? canonicalTextFallback)
    {
        var word = ResourceCandidateFieldHelper.GetFieldCI(fields, "word", "lemma", "headword");
        var definition = ResourceCandidateFieldHelper.GetFieldCI(fields, "definition", "meaning");
        var partOfSpeech = ResourceCandidateFieldHelper.GetFieldCI(fields, "partOfSpeech", "pos");
        var examples = GetFieldArrayCI(fields, "examples");
        var wasLegacy = !HasCanonicalKey(fields, "word") || !HasCanonicalKey(fields, "definition");

        return CandidateContentParseResult.Ok(
            new VocabularyCandidateContent(
                string.IsNullOrWhiteSpace(word) ? canonicalTextFallback ?? string.Empty : word,
                definition ?? string.Empty, partOfSpeech, examples),
            wasLegacy);
    }

    private static CandidateContentParseResult ParseGrammar(IReadOnlyDictionary<string, string?> fields, string? canonicalTextFallback)
    {
        var title = ResourceCandidateFieldHelper.GetFieldCI(fields, "grammarKey", "title");
        var explanation = ResourceCandidateFieldHelper.GetFieldCI(fields, "explanation");
        var examples = GetFieldArrayCI(fields, "examples");
        var commonMistakes = GetFieldArrayCI(fields, "commonMistakes");
        var wasLegacy = !HasCanonicalKey(fields, "title") || !HasCanonicalKey(fields, "explanation");

        return CandidateContentParseResult.Ok(
            new GrammarCandidateContent(
                string.IsNullOrWhiteSpace(title) ? canonicalTextFallback ?? string.Empty : title,
                explanation ?? string.Empty, examples, commonMistakes),
            wasLegacy);
    }

    private static CandidateContentParseResult ParseReading(IReadOnlyDictionary<string, string?> fields, string? canonicalTextFallback)
    {
        var passage = ResourceCandidateFieldHelper.GetFieldCI(fields, "passageText", "passage", "text");
        var title = ResourceCandidateFieldHelper.GetFieldCI(fields, "title");
        var textType = ResourceCandidateFieldHelper.GetFieldCI(fields, "textType", "type");
        var referenceSource = ResourceCandidateFieldHelper.GetFieldCI(fields, "referenceSource", "source");
        var wasLegacy = !HasCanonicalKey(fields, "passageText");

        return CandidateContentParseResult.Ok(
            new ReadingCandidateContent(
                string.IsNullOrWhiteSpace(passage) ? canonicalTextFallback ?? string.Empty : passage,
                title, textType, referenceSource),
            wasLegacy);
    }

    private static CandidateContentParseResult ParseListening(IReadOnlyDictionary<string, string?> fields, string? canonicalTextFallback)
    {
        var title = ResourceCandidateFieldHelper.GetFieldCI(fields, "title");
        var transcript = ResourceCandidateFieldHelper.GetFieldCI(fields, "transcript");
        var wasLegacy = !HasCanonicalKey(fields, "title");

        return CandidateContentParseResult.Ok(
            new ListeningCandidateContent(
                string.IsNullOrWhiteSpace(title) ? canonicalTextFallback ?? string.Empty : title, transcript),
            wasLegacy);
    }

    private static CandidateContentParseResult ParseSpeaking(IReadOnlyDictionary<string, string?> fields, string? canonicalTextFallback)
    {
        var promptTextRaw = ResourceCandidateFieldHelper.GetFieldCI(fields, "promptText", "scenario");
        var promptText = string.IsNullOrWhiteSpace(promptTextRaw) ? canonicalTextFallback ?? string.Empty : promptTextRaw;
        var titleRaw = ResourceCandidateFieldHelper.GetFieldCI(fields, "title");
        var title = string.IsNullOrWhiteSpace(titleRaw)
            ? (promptText.Length <= 80 ? promptText : promptText[..80].Trim() + "…")
            : titleRaw;
        var instructions = ResourceCandidateFieldHelper.GetFieldCI(fields, "instructions");
        var context = ResourceCandidateFieldHelper.GetFieldCI(fields, "context");
        var duration = ParsePositiveInt(ResourceCandidateFieldHelper.GetFieldCI(fields, "suggestedDurationSeconds", "durationSeconds"));
        var wasLegacy = !HasCanonicalKey(fields, "promptText");

        return CandidateContentParseResult.Ok(
            new SpeakingCandidateContent(title, promptText, instructions, context, duration), wasLegacy);
    }

    private static CandidateContentParseResult ParseWriting(IReadOnlyDictionary<string, string?> fields, string? canonicalTextFallback)
    {
        var promptTextRaw = ResourceCandidateFieldHelper.GetFieldCI(fields, "promptText", "prompt");
        var promptText = string.IsNullOrWhiteSpace(promptTextRaw) ? canonicalTextFallback ?? string.Empty : promptTextRaw;
        var titleRaw = ResourceCandidateFieldHelper.GetFieldCI(fields, "title");
        var title = string.IsNullOrWhiteSpace(titleRaw)
            ? (promptText.Length <= 80 ? promptText : promptText[..80].Trim() + "…")
            : titleRaw;
        var instructions = ResourceCandidateFieldHelper.GetFieldCI(fields, "instructions");
        var genre = ResourceCandidateFieldHelper.GetFieldCI(fields, "genre", "taskType");
        var minWords = ParsePositiveInt(ResourceCandidateFieldHelper.GetFieldCI(fields, "suggestedMinWords", "minWords"));
        var expectedLevel = ResourceCandidateFieldHelper.GetFieldCI(fields, "expectedLevel");
        var wasLegacy = !HasCanonicalKey(fields, "promptText");

        return CandidateContentParseResult.Ok(
            new WritingCandidateContent(title, promptText, instructions, genre, minWords, expectedLevel), wasLegacy);
    }

    private static bool HasCanonicalKey(IReadOnlyDictionary<string, string?> fields, string canonicalName) =>
        fields.Keys.Any(k => string.Equals(k, canonicalName, StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrWhiteSpace(fields[fields.Keys.First(k => string.Equals(k, canonicalName, StringComparison.OrdinalIgnoreCase))]);

    private static int? ParsePositiveInt(string? raw) =>
        int.TryParse(raw?.Trim(), out var value) && value > 0 ? value : null;

    private static IReadOnlyList<string>? GetFieldArrayCI(IReadOnlyDictionary<string, string?> fields, string fieldName)
    {
        var raw = ResourceCandidateFieldHelper.GetFieldCI(fields, fieldName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var array = JsonSerializer.Deserialize<string[]>(raw);
            return array is { Length: > 0 } ? array : null;
        }
        catch (JsonException)
        {
            // Not a JSON array — treat the whole raw value as a single example/entry rather than
            // dropping it silently.
            return new[] { raw };
        }
    }
}
