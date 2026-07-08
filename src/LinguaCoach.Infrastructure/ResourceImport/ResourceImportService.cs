using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ResourceImport;
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
/// </summary>
public sealed class ResourceImportService : IResourceImportService
{
    // Conservative ceiling for a hand-authored/curated resource file. No existing upload
    // convention in this codebase targets structured-data files (the closest precedent —
    // speaking-audio uploads — allows up to 20MB, but that's binary audio; a 5MB text file is
    // already tens of thousands of rows, comfortably beyond what a single admin import run
    // should contain before it's split up).
    public const int MaxFileSizeBytes = 5 * 1024 * 1024;

    // Unicode ranges used by Persian/Arabic-script text. Used only as a conservative reject
    // signal for gate 1 — this is NOT a language-identification library. Limitation: does not
    // detect non-English text written in Latin script (e.g. French, Turkish) — Phase E1 accepts
    // that gap and relies on admins choosing correct sources; a real language-ID pass is future
    // work if needed.
    private static bool IsArabicScriptChar(char c) =>
        (c >= '؀' && c <= 'ۿ') ||
        (c >= 'ݐ' && c <= 'ݿ') ||
        (c >= 'ﭐ' && c <= '﷿') ||
        (c >= 'ﹰ' && c <= '﻿');

    private static readonly string[] ExplicitLanguageFieldNames = { "languagecode", "language", "lang" };
    private static readonly string[] AllowedExplicitLanguageValues = { "en", "eng", "en-us", "en-gb" };

    private static readonly string[] VocabularyFields = { "word", "lemma" };
    private static readonly string[] GrammarFields = { "grammarkey", "explanation" };
    private static readonly string[] ReadingFields = { "passage", "text" };
    private static readonly string[] TemplateFields = { "formio", "schema", "template" };
    private static readonly string[] TitleFields = { "title" };
    private static readonly string[] AnyContentFields =
        { "word", "lemma", "text", "passage", "title", "grammarkey", "explanation", "formio", "schema", "template" };

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
                ProcessRow(run.Id, row, seenHashes, ref succeeded, ref rejected, ref warnings);
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

        var (candidateType, canonicalText) = InferCandidateType(row);

        var record = new ResourceRawRecord(
            runId, rawHash, languageVerdict.DetectedLanguageCode, "row",
            rawJson: rawJson);
        record.MarkParsed();
        _db.ResourceRawRecords.Add(record);

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
        _db.ResourceCandidates.Add(candidate);

        succeeded++;
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

        // No explicit language field — fall back to a conservative script/character heuristic.
        // Limitation: this only catches Persian/Arabic-script text and a high proportion of
        // non-Latin characters; it will not catch non-English text written in Latin script.
        var allText = string.Join(' ', row.Values.Where(v => !string.IsNullOrEmpty(v)));
        if (allText.Any(IsArabicScriptChar))
            return new LanguageVerdict(false, "fa", "Row contains Persian/Arabic-script text.");

        var letters = allText.Where(char.IsLetter).ToList();
        if (letters.Count > 0)
        {
            var nonBasicLatin = letters.Count(c => c > 'ɏ'); // beyond Latin Extended-B
            if ((double)nonBasicLatin / letters.Count > 0.15)
                return new LanguageVerdict(false, "unknown", "Row's text is predominantly non-Latin-script.");
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
