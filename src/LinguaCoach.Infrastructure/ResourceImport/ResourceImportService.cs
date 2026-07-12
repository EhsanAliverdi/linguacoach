using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase E1 English resource import pipeline: source approval + license gate → parse (CSV/JSON/
/// JSONL) → per-row gates (duplicate, English-only, recognizable-content) → stage
/// <see cref="ResourceRawRecord"/>/<see cref="ResourceCandidate"/> rows. Never writes to any
/// published Cefr* bank table. No AI analysis, no CEFR classification — those are Phase E2+.
///
/// Phase E6 addition: when a row already carries its own <c>cefrLevel</c>/<c>skill</c>/
/// <c>subskill</c>/<c>tags</c> columns (e.g. an internally-authored seed pack where the author
/// already knows the correct classification), <see cref="ProcessRow"/> copies those values
/// straight onto the new <see cref="ResourceCandidate"/> via <see cref="ResourceCandidate.ApplyAnalysis"/>
/// at staging time. This is a deterministic, content-driven mapping — explicitly distinct from
/// Phase E2's <see cref="ResourceCandidateAnalysisService"/>, which calls a real AI provider to
/// *guess* classification for content whose CEFR level/skill is not already known. A row with no
/// such columns is left exactly as before (null classification fields, unchanged until Phase E2
/// analysis runs).
/// </summary>
public sealed class ResourceImportService : IResourceImportService
{
    // Conservative ceiling for a hand-authored/curated resource file. No existing upload
    // convention in this codebase targets structured-data files (the closest precedent —
    // speaking-audio uploads — allows up to 20MB, but that's binary audio; a 5MB text file is
    // already tens of thousands of rows, comfortably beyond what a single admin import run
    // should contain before it's split up).
    public const int MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly string[] ExplicitLanguageFieldNames = { "languagecode", "language", "lang" };
    private static readonly string[] AllowedExplicitLanguageValues = { "en", "eng", "en-us", "en-gb" };

    private static readonly string[] VocabularyFields = { "word", "lemma" };
    private static readonly string[] GrammarFields = { "grammarkey", "explanation" };
    private static readonly string[] ReadingFields = { "passage", "text" };
    private static readonly string[] TemplateFields = { "formio", "schema", "template" };
    private static readonly string[] TitleFields = { "title" };
    // Phase J5a
    private static readonly string[] WritingFields = { "prompt" };
    private static readonly string[] AnyContentFields =
        { "word", "lemma", "text", "passage", "title", "grammarkey", "explanation", "formio", "schema", "template", "prompt" };

    // Phase E6 — optional deterministic classification columns. When present on a row, their
    // values are copied directly onto the staged candidate (see ProcessRow) instead of being left
    // null for Phase E2's AI analysis to fill in later.
    private const string CefrLevelField = "cefrlevel";
    private const string SkillField = "skill";
    private const string SubskillField = "subskill";
    private const string TagsField = "tags";
    // Phase E8 — two more optional deterministic columns. Like the E6 columns above, these are only
    // read when the row actually carries them (internally-authored packs whose author already knows
    // the correct focus tags / difficulty band), so every prior import path is completely unchanged
    // (focus tags stay "[]", difficulty band stays null when the columns are absent). Only
    // CefrReadingPassage persists these downstream — the other Cefr* bank entities do not carry
    // focus-tag or difficulty-band columns, so mapping them is harmless for those types.
    private const string FocusTagsField = "focustags";
    private const string DifficultyBandField = "difficultyband";

    private readonly LinguaCoachDbContext _db;
    private readonly IActivityContentFingerprintService _fingerprint;

    public ResourceImportService(LinguaCoachDbContext db, IActivityContentFingerprintService fingerprint)
    {
        _db = db;
        _fingerprint = fingerprint;
    }

    public async Task<ResourceImportResult> ImportAsync(ResourceImportRequest request, CancellationToken ct = default)
    {
        var source = await _db.CefrResourceSources.FirstOrDefaultAsync(s => s.Id == request.SourceId, ct)
            ?? throw new ResourceImportValidationException($"Resource source '{request.SourceId}' was not found.");

        // Gate 2 — license/source approval. Blocks BEFORE any run row is created: an import
        // attempt against a non-approved or non-English source is a configuration/process error
        // by the caller, not a data-quality issue worth recording a run for.
        if (!source.IsImportApproved)
            throw new ResourceImportValidationException(
                $"Resource source '{source.Name}' is not approved for import.");
        if (!string.Equals(source.LanguageCode, CefrResourceSource.RequiredLanguageCode, StringComparison.OrdinalIgnoreCase))
            throw new ResourceImportValidationException(
                $"Resource source '{source.Name}' is not English ('{source.LanguageCode}') — cannot be imported.");

        using var memoryStream = new MemoryStream();
        await request.FileStream.CopyToAsync(memoryStream, ct);
        if (memoryStream.Length > MaxFileSizeBytes)
            throw new ResourceImportValidationException(
                $"File exceeds the maximum import size of {MaxFileSizeBytes / (1024 * 1024)}MB.");
        if (memoryStream.Length == 0)
            throw new ResourceImportValidationException("File is empty.");

        var bytes = memoryStream.ToArray();
        var fileHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var fileText = Encoding.UTF8.GetString(bytes);

        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = new ResourceImportRun(
            source.Id, request.ImportMode, request.FileName, fileHash, startedAtUtc,
            request.ImportedByUserId, source.SourceVersion, request.Notes);
        _db.ResourceImportRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        List<IReadOnlyDictionary<string, string?>> rows;
        try
        {
            rows = ParseRows(fileText, request.ImportMode);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            // The file itself is fundamentally unreadable — nothing structured to salvage.
            run.MarkFailed($"File could not be parsed as {request.ImportMode}: {ex.Message}", DateTimeOffset.UtcNow);
            await _db.SaveChangesAsync(ct);
            return ToResult(run);
        }

        var total = 0;
        var succeeded = 0;
        var rejected = 0;
        var warnings = 0;
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            total++;
            try
            {
                ProcessRow(run.Id, row, seenHashes, request, ref succeeded, ref rejected, ref warnings);
            }
            catch (Exception)
            {
                // Continue-on-error per row (matches PracticeGymGenerationJob's per-item
                // try/catch convention) — one malformed row must never abort the whole run.
                rejected++;
                warnings++;
                var warningsJson = JsonSerializer.Serialize(new[] { "Row could not be processed due to an unexpected error." });
                var fallbackRecord = new ResourceRawRecord(
                    run.Id, Guid.NewGuid().ToString("N"), "unknown", request.ImportMode.ToString(),
                    extractionWarningsJson: warningsJson);
                fallbackRecord.MarkRejected(warningsJson);
                _db.ResourceRawRecords.Add(fallbackRecord);
            }
        }

        await _db.SaveChangesAsync(ct);

        var completedAtUtc = DateTimeOffset.UtcNow;
        var errorSummary = succeeded == 0 && total > 0
            ? $"All {total} row(s) were rejected — see raw record warnings for details."
            : null;
        run.Complete(total, succeeded, rejected, warnings, completedAtUtc, errorSummary);

        // Only stamp the source's own ImportedAtUtc once real parsing/staging happened.
        if (total > 0)
            source.RecordImport(completedAtUtc);

        await _db.SaveChangesAsync(ct);

        return ToResult(run);
    }

    private void ProcessRow(
        Guid runId,
        IReadOnlyDictionary<string, string?> row,
        HashSet<string> seenHashesInRun,
        ResourceImportRequest request,
        ref int succeeded,
        ref int rejected,
        ref int warnings)
    {
        var rawJson = JsonSerializer.Serialize(row);
        var rawHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            NormalizeForHash(rawJson)))).ToLowerInvariant();

        // Gate — within-run duplicate detection.
        if (!seenHashesInRun.Add(rawHash))
        {
            RejectRow(runId, row, rawJson, rawHash, "duplicate", "Duplicate row within this import run.",
                ref rejected, ref warnings);
            return;
        }

        // Gate 1 — English-only.
        var languageVerdict = EvaluateLanguage(row);
        if (!languageVerdict.IsEnglish)
        {
            RejectRow(runId, row, rawJson, rawHash, languageVerdict.DetectedLanguageCode,
                languageVerdict.RejectReason!, ref rejected, ref warnings);
            return;
        }

        // Gate 3 — must have at least one recognizable content field.
        if (!AnyContentFields.Any(f => HasField(row, f)))
        {
            RejectRow(runId, row, rawJson, rawHash, languageVerdict.DetectedLanguageCode,
                "No recognizable content field (word/lemma/text/passage/title/grammarKey/formIo/schema/template).",
                ref rejected, ref warnings);
            return;
        }

        // Phase H2 — an admin-chosen "broad resource type" is the primary import type for the
        // whole run: it always wins over per-row field-name inference. Row inference (below)
        // remains the sole behavior for every existing file-upload caller that leaves it null.
        var (candidateType, canonicalText) = request.DefaultCandidateType is { } forcedType
            ? (forcedType, ExtractCanonicalTextForType(row, forcedType))
            : InferCandidateType(row);

        var metadata = MergeRowMetadata(row, request);
        var recordWarningsJson = metadata.Warnings.Count > 0
            ? JsonSerializer.Serialize(metadata.Warnings)
            : null;

        var record = new ResourceRawRecord(
            runId, rawHash, languageVerdict.DetectedLanguageCode, "row",
            rawJson: rawJson, extractionWarningsJson: recordWarningsJson);
        record.MarkParsed();
        _db.ResourceRawRecords.Add(record);
        warnings += metadata.Warnings.Count;

        var normalizedJson = JsonSerializer.Serialize(
            row.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value));
        var searchText = string.Join(' ', row.Values.Where(v => !string.IsNullOrWhiteSpace(v))).ToLowerInvariant();

        var fingerprint = _fingerprint.ComputeFingerprint(new ActivityContentFingerprintRequest(
            ContentJson: normalizedJson,
            ContentShape: ActivityContentShape.Unknown,
            CefrLevel: null,
            TopicKey: canonicalText));

        var candidate = new ResourceCandidate(
            record.Id, candidateType, canonicalText, normalizedJson, languageVerdict.DetectedLanguageCode,
            searchText, fingerprint, ResourceCandidateValidationStatus.NeedsReview);
        ApplyMergedMetadata(candidate, metadata);
        _db.ResourceCandidates.Add(candidate);

        succeeded++;
    }

    /// <summary>Phase H2 — canonical-text extraction used only when the admin has forced a
    /// candidate type via <see cref="ResourceImportRequest.DefaultCandidateType"/>, bypassing
    /// <see cref="InferCandidateType"/>'s field-name detection entirely. Tries the same field
    /// names <see cref="InferCandidateType"/> would look for on that type, then falls back to
    /// the first non-empty value on the row (e.g. a pasted line staged under a generic
    /// <c>text</c> column).</summary>
    private static string ExtractCanonicalTextForType(IReadOnlyDictionary<string, string?> row, ResourceCandidateType type)
    {
        string? text = type switch
        {
            ResourceCandidateType.VocabularyEntry =>
                GetField(row, "word") ?? GetField(row, "lemma") ?? GetField(row, "text"),
            ResourceCandidateType.GrammarProfileEntry =>
                GetField(row, "grammarkey") ?? GetField(row, "title") ?? GetField(row, "explanation") ?? GetField(row, "text"),
            ResourceCandidateType.ReadingPassage =>
                GetField(row, "title") ?? GetField(row, "passage") ?? GetField(row, "text"),
            ResourceCandidateType.ActivityTemplateCandidate =>
                GetField(row, "title") ?? GetField(row, "formio") ?? GetField(row, "schema") ?? GetField(row, "template") ?? GetField(row, "text"),
            ResourceCandidateType.WritingPrompt =>
                GetField(row, "title") ?? GetField(row, "prompt") ?? GetField(row, "text"),
            _ => GetField(row, "title") ?? GetField(row, "text"),
        };

        return text ?? row.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "(unknown)";
    }

    private readonly record struct MergedRowMetadata(
        string? CefrLevel, string? Skill, string? Subskill, string? ContextTagsJson,
        string? FocusTagsJson, int? DifficultyBand, IReadOnlyList<string> Warnings)
    {
        public bool HasAnyValue => CefrLevel is not null || Skill is not null || Subskill is not null
            || ContextTagsJson is not null || FocusTagsJson is not null || DifficultyBand is not null;
    }

    /// <summary>
    /// Phase E6/E8/H2 — merges a row's own <c>cefrLevel</c>/<c>skill</c>/<c>subskill</c>/
    /// <c>tags</c>/<c>focusTags</c>/<c>difficultyBand</c> columns (E6/E8) with the import
    /// request's Phase H2 defaults, row value always winning when both are present. A row's own
    /// <c>cefrLevel</c> that isn't one of <see cref="CefrLevelConstants.All"/> is treated as
    /// absent (falls back to the default, if any) and produces a warning — never rejects the row.
    /// Returns a struct with everything <see cref="ApplyMergedMetadata"/> and the raw-record
    /// warnings list need; <see cref="MergedRowMetadata.HasAnyValue"/> is false (no-op) when
    /// neither the row nor the request supplies any classification value — exactly the pre-H2
    /// behavior for every existing file-upload caller.
    /// </summary>
    private static MergedRowMetadata MergeRowMetadata(IReadOnlyDictionary<string, string?> row, ResourceImportRequest request)
    {
        var warnings = new List<string>();

        var rowCefr = GetField(row, CefrLevelField);
        string? cefrLevel;
        if (rowCefr is not null)
        {
            if (CefrLevelConstants.IsValid(rowCefr))
            {
                cefrLevel = rowCefr.Trim().ToUpperInvariant();
            }
            else
            {
                warnings.Add($"Row's cefrLevel '{rowCefr}' is not a valid CEFR level — falling back to the import default.");
                cefrLevel = ValidatedDefaultCefr(request.DefaultCefrLevel, warnings);
            }
        }
        else
        {
            cefrLevel = ValidatedDefaultCefr(request.DefaultCefrLevel, warnings);
        }

        var skill = GetField(row, SkillField) ?? request.DefaultSkill;
        var subskill = GetField(row, SubskillField) ?? request.DefaultSubskill;
        var tags = GetField(row, TagsField)
            ?? (request.DefaultContextTags is { Count: > 0 } ? JsonSerializer.Serialize(request.DefaultContextTags) : null);
        var focusTags = GetField(row, FocusTagsField)
            ?? (request.DefaultFocusTags is { Count: > 0 } ? JsonSerializer.Serialize(request.DefaultFocusTags) : null);
        var difficultyBand = ParseDifficultyBand(GetField(row, DifficultyBandField)) ?? request.DefaultDifficultyBand;

        return new MergedRowMetadata(cefrLevel, skill, subskill, tags, focusTags, difficultyBand, warnings);
    }

    private static string? ValidatedDefaultCefr(string? defaultCefr, List<string> warnings)
    {
        if (defaultCefr is null) return null;
        if (CefrLevelConstants.IsValid(defaultCefr)) return defaultCefr.Trim().ToUpperInvariant();
        warnings.Add($"Import default CEFR level '{defaultCefr}' is not valid — no CEFR level applied to this row.");
        return null;
    }

    /// <summary>Writes a <see cref="MergeRowMetadata"/> result onto the candidate via
    /// <see cref="ResourceCandidate.ApplyAnalysis"/>. Confidence is stamped at 1.0 whenever a
    /// CEFR level is present — this is a value the row's author or the importing admin already
    /// asserts, not a probabilistic AI guess. No-op when nothing was merged.</summary>
    private static void ApplyMergedMetadata(ResourceCandidate candidate, MergedRowMetadata metadata)
    {
        if (!metadata.HasAnyValue) return;

        var mappingJson = JsonSerializer.Serialize(new
        {
            mappingSource = "import-row-deterministic-mapping",
            cefrLevel = metadata.CefrLevel,
            skill = metadata.Skill,
            subskill = metadata.Subskill,
            difficultyBand = metadata.DifficultyBand
        });

        candidate.ApplyAnalysis(
            aiAnalysisJson: mappingJson,
            cefrLevel: metadata.CefrLevel,
            cefrConfidence: metadata.CefrLevel is null ? null : 1.0,
            primarySkill: metadata.Skill,
            subskill: metadata.Subskill,
            difficultyBand: metadata.DifficultyBand,
            contextTagsJson: metadata.ContextTagsJson ?? "[]",
            focusTagsJson: metadata.FocusTagsJson ?? "[]",
            grammarTagsJson: null,
            vocabularyTagsJson: null,
            pronunciationTagsJson: null,
            activitySuitabilityTagsJson: null,
            safetyTagsJson: null,
            qualityScore: null,
            searchText: null);
    }

    /// <summary>Phase E8 — parses an optional <c>difficultyBand</c> row value to the 1-5 band the
    /// bank entities accept. Any missing, non-numeric, or out-of-range value returns null (the row
    /// simply carries no difficulty band), never throws — a malformed metadata column must never
    /// abort staging.</summary>
    private static int? ParseDifficultyBand(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return int.TryParse(raw.Trim(), out var band) && band is >= 1 and <= 5 ? band : null;
    }

    private void RejectRow(
        Guid runId, IReadOnlyDictionary<string, string?> row, string rawJson, string rawHash,
        string detectedLanguageCode, string reason, ref int rejected, ref int warnings)
    {
        var warningsJson = JsonSerializer.Serialize(new[] { reason });
        var record = new ResourceRawRecord(
            runId, rawHash, detectedLanguageCode, "row", rawJson: rawJson, extractionWarningsJson: warningsJson);
        record.MarkRejected(warningsJson);
        _db.ResourceRawRecords.Add(record);
        rejected++;
        warnings++;
    }

    private static (ResourceCandidateType Type, string CanonicalText) InferCandidateType(
        IReadOnlyDictionary<string, string?> row)
    {
        if (VocabularyFields.Any(f => HasField(row, f)))
            return (ResourceCandidateType.VocabularyEntry, GetField(row, "word") ?? GetField(row, "lemma")!);

        if (GrammarFields.Any(f => HasField(row, f)) || (HasField(row, "title") && HasField(row, "explanation")))
            return (ResourceCandidateType.GrammarProfileEntry,
                GetField(row, "grammarkey") ?? GetField(row, "title") ?? GetField(row, "explanation")!);

        if (HasField(row, "title") && (HasField(row, "text") || HasField(row, "passage")))
            return (ResourceCandidateType.ReadingPassage, GetField(row, "title")!);

        if (ReadingFields.Any(f => HasField(row, f)))
            return (ResourceCandidateType.ReadingPassage,
                GetField(row, "passage") ?? GetField(row, "text")!);

        // Phase J5a — checked before TemplateFields since neither field set overlaps.
        if (WritingFields.Any(f => HasField(row, f)))
            return (ResourceCandidateType.WritingPrompt, GetField(row, "title") ?? GetField(row, "prompt")!);

        if (TemplateFields.Any(f => HasField(row, f)))
            return (ResourceCandidateType.ActivityTemplateCandidate,
                GetField(row, "title") ?? GetField(row, "formio") ?? GetField(row, "schema") ?? GetField(row, "template")!);

        if (TitleFields.Any(f => HasField(row, f)))
            return (ResourceCandidateType.Unknown, GetField(row, "title")!);

        // Reachable only if a future content field is added to AnyContentFields without a
        // matching branch here — gate 3 already guarantees at least one field is present.
        var firstNonEmpty = row.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "(unknown)";
        return (ResourceCandidateType.Unknown, firstNonEmpty);
    }

    private static bool HasField(IReadOnlyDictionary<string, string?> row, string fieldNameLower) =>
        !string.IsNullOrWhiteSpace(GetField(row, fieldNameLower));

    private static string? GetField(IReadOnlyDictionary<string, string?> row, string fieldNameLower)
    {
        foreach (var kv in row)
            if (string.Equals(kv.Key, fieldNameLower, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return null;
    }

    private readonly record struct LanguageVerdict(bool IsEnglish, string DetectedLanguageCode, string? RejectReason);

    private static LanguageVerdict EvaluateLanguage(IReadOnlyDictionary<string, string?> row)
    {
        foreach (var fieldName in ExplicitLanguageFieldNames)
        {
            var value = GetField(row, fieldName);
            if (string.IsNullOrWhiteSpace(value)) continue;

            var normalized = value.Trim().ToLowerInvariant();
            return AllowedExplicitLanguageValues.Contains(normalized)
                ? new LanguageVerdict(true, "en", null)
                : new LanguageVerdict(false, normalized, $"Row's explicit language field is '{value}', not English.");
        }

        // No explicit language field — fall back to the shared conservative script/character
        // heuristic (also used by Phase E2's ResourceCandidateValidationService, see
        // ResourceLanguageHeuristic for the documented limitation).
        var allText = string.Join(' ', row.Values.Where(v => !string.IsNullOrEmpty(v)));
        if (ResourceLanguageHeuristic.LooksNonEnglish(allText, out var reason))
        {
            var detected = reason!.Contains("Arabic", StringComparison.OrdinalIgnoreCase) ? "fa" : "unknown";
            return new LanguageVerdict(false, detected, $"Row {(detected == "fa" ? "contains Persian/Arabic-script text." : "is predominantly non-Latin-script.")}");
        }

        return new LanguageVerdict(true, "en", null);
    }

    private static List<IReadOnlyDictionary<string, string?>> ParseRows(string fileText, ResourceImportMode mode)
    {
        return mode switch
        {
            ResourceImportMode.Csv => ParseCsvRows(fileText),
            ResourceImportMode.Json => ParseJsonRows(fileText),
            ResourceImportMode.Jsonl => ParseJsonlRows(fileText),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported import mode.")
        };
    }

    private static List<IReadOnlyDictionary<string, string?>> ParseCsvRows(string fileText)
    {
        var (header, dataRows) = SimpleCsvParser.Parse(fileText);
        if (header.Count == 0)
            throw new FormatException("CSV file has no header row.");

        var result = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var dataRow in dataRows)
        {
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Count; i++)
                dict[header[i]] = i < dataRow.Count ? dataRow[i] : null;
            result.Add(dict);
        }
        return result;
    }

    private static List<IReadOnlyDictionary<string, string?>> ParseJsonRows(string fileText)
    {
        using var doc = JsonDocument.Parse(fileText);
        var result = new List<IReadOnlyDictionary<string, string?>>();

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in doc.RootElement.EnumerateArray())
                result.Add(JsonElementToDict(element));
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            result.Add(JsonElementToDict(doc.RootElement));
        }
        else
        {
            throw new FormatException("Top-level JSON must be an array or an object.");
        }

        return result;
    }

    private static List<IReadOnlyDictionary<string, string?>> ParseJsonlRows(string fileText)
    {
        var result = new List<IReadOnlyDictionary<string, string?>>();
        var lines = fileText.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("Each JSONL line must be a JSON object.");
            result.Add(JsonElementToDict(doc.RootElement));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string?> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => prop.Value.GetRawText()
            };
        }
        return dict;
    }

    private static string NormalizeForHash(string rawJson) => rawJson.Trim().ToLowerInvariant();

    private static ResourceImportResult ToResult(ResourceImportRun run) => new(
        run.Id, run.Status.ToString(), run.TotalRecordCount, run.SucceededCount,
        run.RejectedCount, run.WarningCount, run.ErrorSummary);
}
