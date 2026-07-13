using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase E3 — read-only rendered preview for a staged <see cref="ResourceCandidate"/>. Builds a
/// per-candidate-type "what the student would see" projection plus an admin-only provenance/
/// validation/AI-analysis summary. Never mutates the candidate (no <c>UpdatedAtUtc</c> bump, no
/// <c>SaveChangesAsync</c> call anywhere in this service) and never writes to any published
/// Cefr* bank table — that stays Phase E4 (publish), entirely out of scope here.
///
/// A degraded/unsupported preview (missing expected fields for the candidate's type, an
/// unparsable Form.io schema, an unrecognized candidate type) is informational, not an error:
/// <see cref="ResourceCandidatePreviewDto.CanPreview"/> is set false and a
/// <see cref="ResourceCandidatePreviewDto.PreviewWarnings"/> entry explains why, but whatever
/// generic info is available (canonical text, source/license, tags) is still returned.
/// </summary>
public sealed class ResourceCandidatePreviewService : IResourceCandidatePreviewService
{
    // Judgment call: bounded excerpt lengths so a preview response can never balloon just
    // because one staged row happens to carry an oversized raw payload or field value — mirrors
    // the "keep it bounded" discipline already used by ResourceImportService/
    // ResourceCandidateAnalysisService's own truncation constants.
    private const int MaxRawExcerptLength = 300;
    private const int MaxFieldValueLength = 500;
    private const int MaxFieldSummaryItems = 12;
    private const double AssumedWordsPerMinute = 200;

    private static readonly string[] AdminOnlyActivityMetadataKeys =
    {
        "rubric", "scoringrules", "scoringweight", "answerkey",
        "correctanswer", "correctanswers", "score", "quiz",
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _formIoValidator;

    public ResourceCandidatePreviewService(LinguaCoachDbContext db, IFormIoSchemaValidationService formIoValidator)
    {
        _db = db;
        _formIoValidator = formIoValidator;
    }

    public async Task<ResourceCandidatePreviewDto?> GetPreviewAsync(Guid candidateId, CancellationToken ct = default)
    {
        var loaded = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            join s in _db.CefrResourceSources on run.CefrResourceSourceId equals s.Id
            where c.Id == candidateId
            select new { Candidate = c, RawRecord = r, Run = run, Source = s })
            .FirstOrDefaultAsync(ct);

        if (loaded is null)
            return null;

        var candidate = loaded.Candidate;
        var previewWarnings = new List<string>();
        // Two projections of the same NormalizedJson: `rawFields` keeps full-fidelity values (so
        // e.g. a full reading passage's word count is accurate) and is used only internally to
        // build the rendered preview model; `normalizedContent` is the bounded, safe-to-display
        // projection returned on the DTO (see ResourceCandidatePreviewDto.NormalizedContent).
        var rawFields = ParseFields(candidate.NormalizedJson, truncate: false);
        var normalizedContent = ParseFields(candidate.NormalizedJson, truncate: true);

        var (rendered, canPreview) = BuildRenderedPreview(candidate, rawFields, previewWarnings);

        var (validationErrors, validationWarnings) = ParseValidationSummary(candidate.RejectReason);
        var duplicateIndicators = validationWarnings
            .Where(w => w.StartsWith("Duplicate:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var safetyIssues = ParseJsonStringArray(candidate.SafetyTagsJson);

        var tags = new ResourceCandidateTagsDto(
            ParseJsonStringArray(candidate.ContextTagsJson),
            ParseJsonStringArray(candidate.FocusTagsJson),
            ParseJsonStringArray(candidate.GrammarTagsJson),
            ParseJsonStringArray(candidate.VocabularyTagsJson),
            ParseJsonStringArray(candidate.PronunciationTagsJson),
            ParseJsonStringArray(candidate.ActivitySuitabilityTagsJson));

        ResourceCandidateAiAnalysisSummaryDto? aiSummary = candidate.AiAnalysisJson is null
            ? null
            : new ResourceCandidateAiAnalysisSummaryDto(
                candidate.CefrLevel, candidate.CefrConfidence, candidate.PrimarySkill, candidate.Subskill,
                candidate.DifficultyBand, candidate.QualityScore, safetyIssues);

        var rawExcerptSource = loaded.RawRecord.RawText ?? loaded.RawRecord.RawJson ?? string.Empty;
        var rawSummary = new ResourceCandidateRawRecordSummaryDto(
            loaded.RawRecord.Id, loaded.RawRecord.ExtractionStatus.ToString(),
            Truncate(rawExcerptSource, MaxRawExcerptLength));

        var runSummary = new ResourceCandidateImportRunSummaryDto(
            loaded.Run.Id, loaded.Run.CefrResourceSourceId, loaded.Run.StartedAtUtc,
            loaded.Run.CompletedAtUtc, loaded.Run.Status.ToString());

        var sourceInfo = new ResourceCandidateSourceInfoDto(
            loaded.Source.Id, loaded.Source.Name, loaded.Source.LicenseType, loaded.Source.SourceUrl,
            loaded.Source.DownloadUrl, loaded.Source.AttributionText, loaded.Source.AllowsStudentDisplay,
            loaded.Source.AllowsCommercialUse);

        var adminOnlyMetadataJson = ExtractAdminOnlyActivityMetadata(candidate);
        var title = DeriveTitle(candidate, rawFields);

        return new ResourceCandidatePreviewDto(
            candidate.Id,
            candidate.CandidateType.ToString(),
            title,
            candidate.LanguageCode,
            candidate.CanonicalText,
            normalizedContent,
            rendered,
            sourceInfo,
            candidate.CefrLevel,
            candidate.CefrConfidence,
            candidate.PrimarySkill,
            candidate.Subskill,
            candidate.DifficultyBand,
            tags,
            candidate.QualityScore,
            safetyIssues,
            candidate.ValidationStatus.ToString(),
            validationErrors,
            validationWarnings,
            candidate.ReviewStatus.ToString(),
            candidate.ContentFingerprint,
            duplicateIndicators,
            aiSummary,
            candidate.AiAnalysisJson,
            rawSummary,
            runSummary,
            canPreview,
            previewWarnings,
            adminOnlyMetadataJson);
    }

    // ── Rendered preview model per candidate type ───────────────────────────────

    private (ResourceCandidateRenderedPreviewDto Rendered, bool CanPreview) BuildRenderedPreview(
        ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized, List<string> previewWarnings) =>
        candidate.CandidateType switch
        {
            ResourceCandidateType.VocabularyEntry => BuildVocabularyPreview(candidate, normalized, previewWarnings),
            ResourceCandidateType.GrammarProfileEntry => BuildGrammarPreview(candidate, normalized, previewWarnings),
            ResourceCandidateType.ReadingPassage => BuildReadingPreview(candidate, normalized, previewWarnings),
            ResourceCandidateType.ActivityTemplateCandidate => BuildActivityTemplatePreview(candidate, previewWarnings),
            ResourceCandidateType.WritingPrompt => BuildWritingPreview(candidate, normalized, previewWarnings),
            ResourceCandidateType.ListeningPassage => BuildListeningPreview(candidate, normalized, previewWarnings),
            ResourceCandidateType.SpeakingPrompt => BuildSpeakingPreview(candidate, normalized, previewWarnings),
            _ => BuildUnknownPreview(candidate, normalized, previewWarnings),
        };

    private static (ResourceCandidateRenderedPreviewDto, bool) BuildVocabularyPreview(
        ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized, List<string> previewWarnings)
    {
        var word = GetFieldCI(normalized, "word", "lemma");
        if (word is null)
        {
            previewWarnings.Add("VocabularyEntry candidate has no 'word'/'lemma' field to render — falling back to its canonical text.");
            return (new ResourceCandidateRenderedPreviewDto(
                Kind: ResourceCandidateType.VocabularyEntry.ToString(), Word: candidate.CanonicalText), false);
        }

        return (new ResourceCandidateRenderedPreviewDto(
            Kind: ResourceCandidateType.VocabularyEntry.ToString(),
            Word: word,
            PartOfSpeech: GetFieldCI(normalized, "partofspeech", "pos"),
            Definition: GetFieldCI(normalized, "definition", "meaning"),
            Example: GetFieldCI(normalized, "example")), true);
    }

    private static (ResourceCandidateRenderedPreviewDto, bool) BuildGrammarPreview(
        ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized, List<string> previewWarnings)
    {
        var title = GetFieldCI(normalized, "grammarkey", "title");
        var explanation = GetFieldCI(normalized, "explanation");

        if (title is null && explanation is null)
        {
            previewWarnings.Add("GrammarProfileEntry candidate has no 'grammarKey'/'title'/'explanation' field to render — falling back to its canonical text.");
            return (new ResourceCandidateRenderedPreviewDto(
                Kind: ResourceCandidateType.GrammarProfileEntry.ToString(), GrammarTitle: candidate.CanonicalText), false);
        }

        return (new ResourceCandidateRenderedPreviewDto(
            Kind: ResourceCandidateType.GrammarProfileEntry.ToString(),
            GrammarTitle: title ?? candidate.CanonicalText,
            Explanation: explanation,
            GrammarExamples: ParseExamples(normalized)), true);
    }

    /// <summary>Grammar examples may be staged as a JSON array field ("examples") or a single
    /// plain-string field ("example") — handled defensively, matching neither shape being
    /// preferred over the other by the E1 import pipeline.</summary>
    private static IReadOnlyList<string>? ParseExamples(IReadOnlyDictionary<string, string?> normalized)
    {
        var examplesRaw = GetFieldCI(normalized, "examples");
        if (examplesRaw is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(examplesRaw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                            list.Add(item.GetString()!);
                    }
                    if (list.Count > 0) return list;
                }
            }
            catch (JsonException)
            {
                // Not a JSON array — treat the raw string as a single example below instead.
                return new[] { examplesRaw };
            }
        }

        var single = GetFieldCI(normalized, "example");
        return single is null ? null : new[] { single };
    }

    private static (ResourceCandidateRenderedPreviewDto, bool) BuildReadingPreview(
        ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized, List<string> previewWarnings)
    {
        var title = GetFieldCI(normalized, "title") ?? candidate.CanonicalText;
        var passage = GetFieldCI(normalized, "passage", "text");

        if (passage is null)
        {
            previewWarnings.Add("ReadingPassage candidate has no 'passage'/'text' field to render.");
            return (new ResourceCandidateRenderedPreviewDto(
                Kind: ResourceCandidateType.ReadingPassage.ToString(), Title: title), false);
        }

        var wordCount = passage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var readingMinutes = Math.Max(1, (int)Math.Round(wordCount / AssumedWordsPerMinute, MidpointRounding.AwayFromZero));

        return (new ResourceCandidateRenderedPreviewDto(
            Kind: ResourceCandidateType.ReadingPassage.ToString(),
            Title: title,
            PassageText: passage,
            WordCount: wordCount,
            EstimatedReadingMinutes: readingMinutes), true);
    }

    private static (ResourceCandidateRenderedPreviewDto, bool) BuildWritingPreview(
        ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized, List<string> previewWarnings)
    {
        var promptText = GetFieldCI(normalized, "prompt");
        if (promptText is null)
        {
            previewWarnings.Add("WritingPrompt candidate has no 'prompt' field to render — falling back to its canonical text.");
            return (new ResourceCandidateRenderedPreviewDto(
                Kind: ResourceCandidateType.WritingPrompt.ToString(), Title: candidate.CanonicalText), false);
        }

        var title = GetFieldCI(normalized, "title") ?? candidate.CanonicalText;
        var minWords = int.TryParse(GetFieldCI(normalized, "minwords", "suggestedminwords"), out var w) && w > 0 ? w : (int?)null;

        return (new ResourceCandidateRenderedPreviewDto(
            Kind: ResourceCandidateType.WritingPrompt.ToString(),
            Title: title,
            PromptText: promptText,
            Genre: GetFieldCI(normalized, "genre", "tasktype"),
            SuggestedMinWords: minWords), true);
    }

    private static (ResourceCandidateRenderedPreviewDto, bool) BuildSpeakingPreview(
        ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized, List<string> previewWarnings)
    {
        var promptText = GetFieldCI(normalized, "scenario");
        if (promptText is null)
        {
            previewWarnings.Add("SpeakingPrompt candidate has no 'scenario' field to render — falling back to its canonical text.");
            return (new ResourceCandidateRenderedPreviewDto(
                Kind: ResourceCandidateType.SpeakingPrompt.ToString(), Title: candidate.CanonicalText), false);
        }

        var title = GetFieldCI(normalized, "title") ?? candidate.CanonicalText;
        var duration = int.TryParse(GetFieldCI(normalized, "durationseconds", "suggesteddurationseconds"), out var d) && d > 0 ? d : (int?)null;

        return (new ResourceCandidateRenderedPreviewDto(
            Kind: ResourceCandidateType.SpeakingPrompt.ToString(),
            Title: title,
            PromptText: promptText,
            SuggestedDurationSeconds: duration), true);
    }

    private static (ResourceCandidateRenderedPreviewDto, bool) BuildListeningPreview(
        ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized, List<string> previewWarnings)
    {
        var title = GetFieldCI(normalized, "title") ?? candidate.CanonicalText;
        var transcript = GetFieldCI(normalized, "transcript");
        var hasAudio = !string.IsNullOrWhiteSpace(candidate.AudioStorageKey);

        if (!hasAudio)
            previewWarnings.Add("No audio file has been uploaded for this ListeningPassage candidate yet — it cannot be published until one is.");
        if (transcript is null)
            previewWarnings.Add("ListeningPassage candidate has no 'transcript' field — the audio will have no written transcript.");

        return (new ResourceCandidateRenderedPreviewDto(
            Kind: ResourceCandidateType.ListeningPassage.ToString(),
            Title: title,
            Transcript: transcript,
            HasAudio: hasAudio), hasAudio);
    }

    /// <summary>
    /// ActivityTemplateCandidate is the one candidate type that carries a real Form.io schema —
    /// the only student-visible render target reused from the shared app-formio-renderer
    /// component. Re-validates the schema via <see cref="IFormIoSchemaValidationService"/>
    /// (defense in depth — ResourceCandidateValidationService already checked this at the last
    /// validation run, but a preview must never trust a schema is still safe without checking
    /// itself) and refuses to expose it at all if that check fails, rather than risk leaking an
    /// unsafe schema into a "what the student would see" panel.
    /// </summary>
    private (ResourceCandidateRenderedPreviewDto, bool) BuildActivityTemplatePreview(
        ResourceCandidate candidate, List<string> previewWarnings)
    {
        var schemaJson = ResourceCandidateFormIoHelper.ExtractFormIoSchemaJson(candidate.NormalizedJson);
        if (schemaJson is null)
        {
            previewWarnings.Add("ActivityTemplateCandidate has no recognizable Form.io schema JSON (expected a 'formio'/'schema'/'template' field).");
            return (new ResourceCandidateRenderedPreviewDto(Kind: ResourceCandidateType.ActivityTemplateCandidate.ToString()), false);
        }

        var validation = _formIoValidator.ValidateSchema(schemaJson);
        if (!validation.IsValid)
        {
            previewWarnings.Add($"Form.io schema failed student-safe validation and cannot be previewed: {validation.Error}");
            return (new ResourceCandidateRenderedPreviewDto(Kind: ResourceCandidateType.ActivityTemplateCandidate.ToString()), false);
        }

        return (new ResourceCandidateRenderedPreviewDto(
            Kind: ResourceCandidateType.ActivityTemplateCandidate.ToString(),
            StudentVisibleFormIoSchemaJson: schemaJson), true);
    }

    private static (ResourceCandidateRenderedPreviewDto, bool) BuildUnknownPreview(
        ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized, List<string> previewWarnings)
    {
        previewWarnings.Add("This candidate type has no specialized preview yet — showing a generic field summary.");

        var fieldSummary = normalized
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Take(MaxFieldSummaryItems)
            .Select(kv => $"{kv.Key}: {Truncate(kv.Value!, 80)}")
            .ToList();

        return (new ResourceCandidateRenderedPreviewDto(
            Kind: ResourceCandidateType.Unknown.ToString(),
            FieldSummary: fieldSummary.Count > 0 ? fieldSummary : new List<string> { candidate.CanonicalText }), false);
    }

    // ── Admin-only scoring/rubric-shaped metadata (ActivityTemplateCandidate only) ─────────────

    /// <summary>
    /// Defensively looks for scoring/rubric/answer-key-shaped top-level fields on an
    /// ActivityTemplateCandidate's raw row, if any were staged alongside the schema. Never
    /// merged into the student-visible render target — surfaced only in the admin-only side of
    /// the preview DTO.
    /// </summary>
    private static string? ExtractAdminOnlyActivityMetadata(ResourceCandidate candidate)
    {
        if (candidate.CandidateType != ResourceCandidateType.ActivityTemplateCandidate)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(candidate.NormalizedJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var found = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (AdminOnlyActivityMetadataKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                    found[prop.Name] = prop.Value.Clone();
            }

            return found.Count == 0 ? null : JsonSerializer.Serialize(found);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── Shared parsing helpers ──────────────────────────────────────────────────

    private static string DeriveTitle(ResourceCandidate candidate, IReadOnlyDictionary<string, string?> normalized) =>
        GetFieldCI(normalized, "title")
        ?? GetFieldCI(normalized, "word", "lemma")
        ?? GetFieldCI(normalized, "grammarkey")
        ?? candidate.CanonicalText;

    private static string? GetFieldCI(IReadOnlyDictionary<string, string?> dict, params string[] fieldNames) =>
        ResourceCandidateFieldHelper.GetFieldCI(dict, fieldNames);

    /// <summary>
    /// Parses NormalizedJson's field-keyed row into a plain dictionary, delegating the actual
    /// field-name-keyed parse to <see cref="ResourceCandidateFieldHelper.ParseFields"/> (Phase E4
    /// factored this out so the publish service can reuse the exact same lookup). When
    /// <paramref name="truncate"/> is true, each value is bounded to
    /// <see cref="MaxFieldValueLength"/> — used only for the DTO's display-safe
    /// <c>NormalizedContent</c> projection. When false, full-fidelity values are returned — used
    /// internally to build the rendered preview model (e.g. an accurate reading passage word
    /// count), never surfaced on the DTO directly in that form.
    /// </summary>
    private static Dictionary<string, string?> ParseFields(string normalizedJson, bool truncate)
    {
        var result = ResourceCandidateFieldHelper.ParseFields(normalizedJson);
        if (!truncate)
            return result;

        foreach (var key in result.Keys.ToList())
        {
            if (result[key] is { } value)
                result[key] = Truncate(value, MaxFieldValueLength);
        }
        return result;
    }

    private static (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ParseValidationSummary(string? rejectReasonJson)
    {
        if (string.IsNullOrWhiteSpace(rejectReasonJson))
            return (Array.Empty<string>(), Array.Empty<string>());

        try
        {
            using var doc = JsonDocument.Parse(rejectReasonJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return (Array.Empty<string>(), Array.Empty<string>());

            return (GetStringArray(doc.RootElement, "errors"), GetStringArray(doc.RootElement, "warnings"));
        }
        catch (JsonException)
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                list.Add(item.GetString()!);
        }
        return list;
    }

    private static IReadOnlyList<string> ParseJsonStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var list = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    list.Add(item.GetString()!);
            }
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
