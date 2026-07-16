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
using Microsoft.Extensions.Options;

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

    private const string OperationType = "candidate_enrich";

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly IImportAiEnrichmentOperationLedger _aiLedger;
    private readonly IAiPricingResolver _pricingResolver;
    private readonly ImportCostEstimationOptions _costOptions;
    private readonly ILogger<ResourceCandidateAnalysisService> _logger;

    public ResourceCandidateAnalysisService(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        IImportAiEnrichmentOperationLedger aiLedger,
        IAiPricingResolver pricingResolver,
        IOptions<ImportCostEstimationOptions> costOptions,
        ILogger<ResourceCandidateAnalysisService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _aiLedger = aiLedger;
        _pricingResolver = pricingResolver;
        _costOptions = costOptions.Value;
        _logger = logger;
    }

    public async Task<ResourceCandidateAnalysisResult> AnalyzeAsync(Guid candidateId, CancellationToken ct = default)
    {
        var loaded = await LoadContextAsync(candidateId, ct);
        if (loaded is null)
            return new ResourceCandidateAnalysisResult(candidateId, false, "Candidate not found.", null, null, null);

        var (candidate, rawRecord, source) = loaded.Value;

        // ── Phase 4.2 — AI enrichment invariant: a candidate may only be analyzed if it traces
        // back to an Import Package whose Import Execution Plan was approved. Defense-in-depth —
        // the only caller left in this codebase (ImportPackageProcessingService, via the batch
        // service) already only runs post-approval, but this keeps the invariant enforced at the
        // application-service level too, not just by "nothing else calls this anymore." ──
        var provenance = await (
            from r in _db.ResourceImportRuns
            where r.Id == rawRecord.ResourceImportRunId && r.ImportPackageId != null
            join p in _db.ImportPackages on r.ImportPackageId equals p.Id
            select new { PackageId = p.Id, ApprovedProfileId = p.ApprovedImportProfileId })
            .FirstOrDefaultAsync(ct);
        if (provenance?.ApprovedProfileId is null)
        {
            return await FailGracefullyAsync(
                candidate,
                "This candidate has no Import Package with an approved Import Execution Plan — AI analysis is blocked.",
                null, null, ct);
        }

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == provenance.PackageId, ct);
        var plan = await _db.ImportProfiles.FirstAsync(p => p.Id == provenance.ApprovedProfileId, ct);
        var processingModeString = package.ProcessingMode?.ToString() ?? "Direct";

        // ── Phase 4.4D — durable ledgering/cost-ceiling enforcement only applies to the billable
        // AI-structuring modes (FullAiAssisted/SampleDriven); a Direct-mode package's candidates
        // are never routed through AI enrichment in production (ImportPackageProcessingService
        // only calls the batch analysis service when ProcessingMode != Direct) — this mirrors that
        // same gate here so a package with no AI-enrichment cost never needs AI pricing configured. ──
        var requiresCostTracking = package.ProcessingMode is not (null or ImportProcessingMode.Direct);

        ImportAiClaimResult? claim = null;
        LinguaCoach.Application.Ai.ResolvedModelPricing? pricing = null;

        if (requiresCostTracking)
        {
            // Fail closed: unresolved pricing must never silently become $0. Resolved against the
            // assumed provider/model (ImportCostEstimationOptions) — the same assumption the STT
            // ledger and the plan-estimate/approval-time checks already use, since the concrete
            // provider that ends up serving the call is resolved independently per feature key
            // inside AiExecutionService and isn't known until after the call.
            pricing = await _pricingResolver.ResolveAsync(_costOptions.AssumedAiProviderName, _costOptions.AssumedAiModelName, ct)
                ?? throw new ImportPricingUnavailableException(
                    _costOptions.AssumedAiProviderName, _costOptions.AssumedAiModelName, "AI candidate enrichment");

            var logicalKey = ImportAiEnrichmentOperationKey.Compute(
                package.Id, candidateId, rawRecord.RawHash, _costOptions.AssumedAiProviderName, _costOptions.AssumedAiModelName,
                AnalyzePromptKey, processingModeString);
            claim = await _aiLedger.ClaimAsync(
                package.Id, plan.Id, candidateId, logicalKey, OperationType,
                _costOptions.AssumedAiProviderName, AnalyzePromptKey, processingModeString, ct);

            if (claim.Outcome == ImportAiClaimOutcome.AlreadySucceeded)
            {
                // Phase 4.4D critical proof — a completed identical operation is reused after
                // retry: no second provider call, no duplicate cost. Re-applies the previously
                // stored, already-parsed output (never the raw AI response body).
                var reused = JsonSerializer.Deserialize<ResourceCandidateAnalysisOutput>(claim.Operation.ResultReferenceJson!)!;
                ApplyOutputToCandidate(candidate, reused, claim.Operation.ResultReferenceJson!);
                await _db.SaveChangesAsync(ct);
                return new ResourceCandidateAnalysisResult(
                    candidateId, true, null, reused, claim.Operation.ProviderName, claim.Operation.ModelName);
            }

            // ── Before any billable provider call: accrued + estimated-next ≤ ceiling. If not,
            // pause rather than call — the claimed (Pending) ledger row is left untouched so it
            // can be re-entered once the ceiling is amended and processing resumes. ──
            var estimatedCost =
                _costOptions.AssumedInputTokensPerCandidate / 1000m * pricing.InputPer1KTokens +
                _costOptions.AssumedOutputTokensPerCandidate / 1000m * pricing.OutputPer1KTokens;
            var ceiling = plan.ApprovedCostCeiling ?? decimal.MaxValue;
            var tolerance = (decimal)_costOptions.CostCeilingToleranceFraction;
            var projected = package.AccruedCost + estimatedCost;
            if (projected > ceiling * (1 + tolerance))
            {
                var pauseReason = $"Projected cost reached ${projected:N2}, at or above the approved ${ceiling:N2} " +
                    $"ceiling, before AI enrichment for candidate {candidateId}.";
                return new ResourceCandidateAnalysisResult(
                    candidateId, false, pauseReason, null, null, null, CeilingReached: true, PauseReason: pauseReason);
            }
        }

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
            return await FailClaimedAsync(claim?.Operation, candidate, $"Could not build AI prompt: {ex.Message}", null, null, ct);
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
            return await FailClaimedAsync(claim?.Operation, candidate, $"AI provider unavailable: {ex.Message}", null, null, ct);
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
                return await FailClaimedAsync(claim?.Operation, candidate, $"AI provider unavailable on retry: {ex.Message}", null, null, ct);
            }

            parsed = TryParseOutput(execResult.ResponseJson, out cleanedJson, out parseError);

            if (parsed is null)
            {
                return await FailClaimedAsync(
                    claim?.Operation, candidate,
                    $"AI response could not be parsed after retry: {parseError}",
                    execResult.ProviderName, execResult.ModelName, ct);
            }
        }

        var resultReferenceJson = JsonSerializer.Serialize(parsed);
        ApplyOutputToCandidate(candidate, parsed, resultReferenceJson);

        if (requiresCostTracking)
        {
            // Actual measured usage when the provider reported it; falls back to the same
            // assumption used for the pre-call ceiling estimate when it didn't (e.g. a fake/test
            // provider).
            var inputTokens = execResult.InputTokens > 0 ? execResult.InputTokens : _costOptions.AssumedInputTokensPerCandidate;
            var outputTokens = execResult.OutputTokens > 0 ? execResult.OutputTokens : _costOptions.AssumedOutputTokensPerCandidate;
            var calculatedCost = inputTokens / 1000m * pricing!.InputPer1KTokens + outputTokens / 1000m * pricing.OutputPer1KTokens;

            await _aiLedger.MarkSucceededAsync(
                claim!.Operation, resultReferenceJson, calculatedCost, "USD", inputTokens, outputTokens,
                pricing.InputPer1KTokens, pricing.OutputPer1KTokens, execResult.ModelName, ct);
            package.AccrueCost(calculatedCost, "USD");
        }

        // Candidate (and, when cost-tracked, ledger row + package cost) — one save, never drifts
        // apart from a crash between separate saves (mirrors the STT ledger's exact discipline).
        await _db.SaveChangesAsync(ct);

        return new ResourceCandidateAnalysisResult(
            candidateId, true, null, parsed, execResult.ProviderName, execResult.ModelName);
    }

    private static void ApplyOutputToCandidate(ResourceCandidate candidate, ResourceCandidateAnalysisOutput parsed, string analysisJson) =>
        candidate.ApplyAnalysis(
            analysisJson,
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

    /// <summary>Marks an already-claimed AI ledger operation Failed (persists immediately, per
    /// <see cref="IImportAiEnrichmentOperationLedger.MarkFailedAsync"/>'s contract), then applies
    /// the same graceful candidate downgrade <see cref="FailGracefullyAsync"/> uses.</summary>
    private async Task<ResourceCandidateAnalysisResult> FailClaimedAsync(
        ImportAiEnrichmentOperation? operation, ResourceCandidate candidate, string errorMessage,
        string? providerName, string? modelName, CancellationToken ct)
    {
        if (operation is not null)
            await _aiLedger.MarkFailedAsync(operation, errorMessage, ct);
        return await FailGracefullyAsync(candidate, errorMessage, providerName, modelName, ct);
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
