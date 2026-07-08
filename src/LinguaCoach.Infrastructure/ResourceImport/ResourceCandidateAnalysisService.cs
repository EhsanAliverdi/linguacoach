using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase E2 — AI analysis of a staged <see cref="ResourceCandidate"/>. Advisory only: stores
/// what the AI suggested (CEFR level/confidence, skill/subskill, difficulty, tags, quality
/// signals) but never itself decides <see cref="ResourceCandidateValidationStatus"/> — that is
/// <see cref="ResourceCandidateValidationService"/>'s exclusive job, run separately.
///
/// Mirrors <see cref="LinguaCoach.Infrastructure.ActivityTemplates.ActivityTemplateInstanceGenerator"/>'s
/// retry-once-on-bad-JSON pattern, with one deliberate difference: that generator is a
/// synchronous student-facing path where failure must surface immediately (so it throws after a
/// failed retry). This service is an offline admin-triggered enrichment step, so any failure —
/// AI unavailable, or bad JSON surviving the retry — degrades to "needs manual review" instead of
/// throwing. The candidate's raw/staged data is never touched on failure.
/// </summary>
public sealed class ResourceCandidateAnalysisService : IResourceCandidateAnalysisService
{
    public const string AnalyzePromptKey = "resource_candidate_analyze";

    // Conservative truncation so a single oversized candidate can't blow the prompt's token
    // budget — matches the spirit of ResourceImportService.MaxFileSizeBytes' "keep it bounded"
    // discipline, applied per-field here instead of per-file.
    private const int MaxTextVariableLength = 4000;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<ResourceCandidateAnalysisService> _logger;

    public ResourceCandidateAnalysisService(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<ResourceCandidateAnalysisService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<ResourceCandidateAnalysisResult> AnalyzeAsync(Guid candidateId, CancellationToken ct = default)
    {
        var loaded = await LoadContextAsync(candidateId, ct);
        if (loaded is null)
            return new ResourceCandidateAnalysisResult(candidateId, false, "Candidate not found.", null, null, null);

        var (candidate, rawRecord, source) = loaded.Value;

        var variables = new Dictionary<string, string>
        {
            ["candidateType"] = candidate.CandidateType.ToString(),
            ["canonicalText"] = Truncate(candidate.CanonicalText),
            ["normalizedJson"] = Truncate(candidate.NormalizedJson),
            ["languageCode"] = candidate.LanguageCode,
            ["sourceName"] = source.Name,
            ["sourceLicense"] = source.LicenseType,
            ["rawContext"] = Truncate(rawRecord.RawText ?? rawRecord.RawJson ?? string.Empty),
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(AnalyzePromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to build resource candidate analysis prompt for {CandidateId} (non-blocking).", candidateId);
            return await FailGracefullyAsync(candidate, $"Could not build AI prompt: {ex.Message}", null, null, ct);
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(AnalyzePromptKey, aiRequest, null, correlationId, ct);
        }
        catch (Exception ex)
        {
            // AiExecutionService already tried primary + fallback internally before throwing —
            // no point retrying the same exhausted pair again here.
            _logger.LogWarning(ex,
                "AI provider unavailable for resource candidate analysis {CandidateId} CorrelationId={CorrelationId} (non-blocking).",
                candidateId, correlationId);
            return await FailGracefullyAsync(candidate, $"AI provider unavailable: {ex.Message}", null, null, ct);
        }

        var parsed = TryParseOutput(execResult.ResponseJson, out var cleanedJson, out var parseError);

        if (parsed is null)
        {
            // Retry exactly once on bad/invalid JSON, same as ActivityTemplateInstanceGenerator.
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(AnalyzePromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "AI provider unavailable on retry for resource candidate analysis {CandidateId} CorrelationId={CorrelationId} (non-blocking).",
                    candidateId, correlationId);
                return await FailGracefullyAsync(candidate, $"AI provider unavailable on retry: {ex.Message}", null, null, ct);
            }

            parsed = TryParseOutput(execResult.ResponseJson, out cleanedJson, out parseError);

            if (parsed is null)
            {
                return await FailGracefullyAsync(
                    candidate,
                    $"AI response could not be parsed after retry: {parseError}",
                    execResult.ProviderName, execResult.ModelName, ct);
            }
        }

        candidate.ApplyAnalysis(
            cleanedJson,
            parsed.CefrLevel,
            parsed.CefrConfidence,
            parsed.PrimarySkill,
            parsed.Subskill,
            parsed.DifficultyBand,
            JsonSerializer.Serialize(parsed.ContextTags),
            JsonSerializer.Serialize(parsed.FocusTags),
            parsed.GrammarTags.Count > 0 ? JsonSerializer.Serialize(parsed.GrammarTags) : null,
            parsed.VocabularyTags.Count > 0 ? JsonSerializer.Serialize(parsed.VocabularyTags) : null,
            parsed.PronunciationTags.Count > 0 ? JsonSerializer.Serialize(parsed.PronunciationTags) : null,
            parsed.ActivitySuitabilityTags.Count > 0 ? JsonSerializer.Serialize(parsed.ActivitySuitabilityTags) : null,
            parsed.SafetyTags.Count > 0 ? JsonSerializer.Serialize(parsed.SafetyTags) : null,
            parsed.QualityScore,
            parsed.SearchText);

        await _db.SaveChangesAsync(ct);

        return new ResourceCandidateAnalysisResult(
            candidateId, true, null, parsed, execResult.ProviderName, execResult.ModelName);
    }

    private async Task<(ResourceCandidate Candidate, ResourceRawRecord RawRecord, CefrResourceSource Source)?> LoadContextAsync(
        Guid candidateId, CancellationToken ct)
    {
        var result = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            join s in _db.CefrResourceSources on run.CefrResourceSourceId equals s.Id
            where c.Id == candidateId
            select new { Candidate = c, RawRecord = r, Source = s })
            .FirstOrDefaultAsync(ct);

        return result is null ? null : (result.Candidate, result.RawRecord, result.Source);
    }

    private async Task<ResourceCandidateAnalysisResult> FailGracefullyAsync(
        ResourceCandidate candidate, string errorMessage, string? providerName, string? modelName, CancellationToken ct)
    {
        // Only promote Pending -> NeedsReview; never downgrade a candidate that already has a
        // real deterministic validation decision (Passed/Failed/NeedsReview) recorded against it.
        // Candidates staged by Phase E1's ResourceImportService already start at NeedsReview, so
        // this branch mainly guards future callers that might construct a Pending candidate.
        if (candidate.ValidationStatus == ResourceCandidateValidationStatus.Pending)
        {
            candidate.ApplyValidation(
                ResourceCandidateValidationStatus.NeedsReview,
                BuildNoteJson(Array.Empty<string>(),
                    new[] { $"AI analysis failed or was unavailable: {errorMessage} Original candidate data was left untouched; safe to re-run analysis later." }));
            await _db.SaveChangesAsync(ct);
        }

        return new ResourceCandidateAnalysisResult(candidate.Id, false, errorMessage, null, providerName, modelName);
    }

    private static string BuildNoteJson(IReadOnlyList<string> errors, IReadOnlyList<string> warnings) =>
        JsonSerializer.Serialize(new { errors, warnings });

    private static string Truncate(string value) =>
        value.Length <= MaxTextVariableLength ? value : value[..MaxTextVariableLength];

    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }
        return cleaned;
    }

    /// <summary>
    /// Fully defensive JSON->output parsing: missing fields become null/default, wrong JSON
    /// value types are ignored (not thrown), cefrConfidence/qualityScore outside [0,1] are
    /// clamped, an invalid cefrLevel string is dropped rather than stored as garbage. Never
    /// throws — returns null (with <paramref name="parseError"/> set) only when the response
    /// isn't even parseable as a JSON object.
    /// </summary>
    private static ResourceCandidateAnalysisOutput? TryParseOutput(
        string rawResponse, out string cleanedJson, out string? parseError)
    {
        cleanedJson = CleanJson(rawResponse);
        parseError = null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(cleanedJson);
        }
        catch (JsonException ex)
        {
            parseError = $"Response is not valid JSON: {ex.Message}";
            return null;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                parseError = "Response is not a JSON object.";
                return null;
            }

            var root = doc.RootElement;

            var cefrLevel = GetString(root, "cefrLevel");
            if (cefrLevel is not null && !CefrLevelConstants.IsValid(cefrLevel))
                cefrLevel = null; // drop invalid CEFR level rather than store garbage

            var cefrConfidence = ClampUnit(GetDouble(root, "cefrConfidence"));
            var qualityScore = ClampUnit(GetDouble(root, "qualityScore"));

            var difficultyBand = GetInt(root, "difficultyBand");
            if (difficultyBand is < 1 or > 5)
                difficultyBand = null;

            return new ResourceCandidateAnalysisOutput(
                cefrLevel,
                cefrConfidence,
                GetString(root, "primarySkill"),
                GetString(root, "subskill"),
                difficultyBand,
                GetStringArray(root, "contextTags"),
                GetStringArray(root, "focusTags"),
                GetStringArray(root, "grammarTags"),
                GetStringArray(root, "vocabularyTags"),
                GetStringArray(root, "pronunciationTags"),
                GetStringArray(root, "activitySuitabilityTags"),
                GetStringArray(root, "safetyTags"),
                qualityScore,
                GetBool(root, "needsHumanReview") ?? false,
                GetStringArray(root, "qualityIssues"),
                GetStringArray(root, "suggestedActivityUses"),
                GetString(root, "searchText"));
        }
    }

    private static double? ClampUnit(double? value) =>
        value is null ? null : Math.Clamp(value.Value, 0d, 1d);

    private static string? GetString(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static double? GetDouble(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)
            ? d
            : null;

    private static int? GetInt(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)
            ? i
            : null;

    private static bool? GetBool(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? el.GetBoolean()
            : null;

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                list.Add(item.GetString()!.Trim());
        }
        return list;
    }
}
