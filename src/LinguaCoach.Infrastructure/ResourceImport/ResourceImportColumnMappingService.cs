using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase K1 — AI-assisted import column-mapping detection. Structurally mirrors
/// <see cref="ResourceCandidateAnalysisService"/> (Phase E2): a single bounded AI call proposes a
/// column-rename mapping for an uploaded/pasted file's header row, retried once on bad JSON, and
/// degrades gracefully (never throws) on any failure — an admin's import must never be blocked by
/// AI unavailability. The AI's proposal is never applied automatically: it is always surfaced to
/// the admin for review/confirmation before an import actually runs (see the frontend's always-shown
/// mapping-review step), and every suggested field is validated against
/// <see cref="ResourceImportRecognizedFields.All"/> before being returned — an AI hallucinating an
/// unrecognized field name is dropped, never trusted.
/// </summary>
public sealed class ResourceImportColumnMappingService : IResourceImportColumnMappingService
{
    public const string ProposeMappingPromptKey = "resource_import_propose_column_mapping";

    // Bounded per AGENTS.md §4 — only the header row + a handful of sample rows are ever sent,
    // never the whole file. Matches ResourceCandidateAnalysisService's per-field truncation spirit.
    private const int MaxSampleRows = 5;
    private const int MaxFieldValueLength = 200;

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<ResourceImportColumnMappingService> _logger;

    public ResourceImportColumnMappingService(
        IAiContextBuilder contextBuilder, AiExecutionService aiExecution, ILogger<ResourceImportColumnMappingService> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<ResourceImportColumnMappingResult> ProposeMappingAsync(
        ResourceImportColumnMappingRequest request, CancellationToken ct = default)
    {
        if (request.Columns.Count == 0)
            return new ResourceImportColumnMappingResult(false, Array.Empty<ColumnMappingSuggestion>(), "No columns to map.");

        var sampleRows = request.SampleRows.Take(MaxSampleRows)
            .Select(row => row.ToDictionary(kv => kv.Key, kv => Truncate(kv.Value)))
            .ToList();

        var variables = new Dictionary<string, string>
        {
            ["columns"] = string.Join(", ", request.Columns),
            ["recognizedFields"] = string.Join(", ", ResourceImportRecognizedFields.All),
            ["sampleRowsJson"] = JsonSerializer.Serialize(sampleRows),
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(ProposeMappingPromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build column-mapping prompt (non-blocking).");
            return Failure($"Could not build AI prompt: {ex.Message}");
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(ProposeMappingPromptKey, aiRequest, null, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider unavailable for column-mapping proposal CorrelationId={CorrelationId} (non-blocking).", correlationId);
            return Failure($"AI provider unavailable: {ex.Message}");
        }

        var parsed = TryParseOutput(execResult.ResponseJson, request.Columns, out var parseError);
        if (parsed is null)
        {
            // Retry exactly once on bad/invalid JSON, same as ResourceCandidateAnalysisService.
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(ProposeMappingPromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider unavailable on retry for column-mapping proposal CorrelationId={CorrelationId} (non-blocking).", correlationId);
                return Failure($"AI provider unavailable on retry: {ex.Message}");
            }

            parsed = TryParseOutput(execResult.ResponseJson, request.Columns, out parseError);
            if (parsed is null)
                return Failure($"AI response could not be parsed after retry: {parseError}");
        }

        return new ResourceImportColumnMappingResult(true, parsed, null);
    }

    private static ResourceImportColumnMappingResult Failure(string message) =>
        new(false, Array.Empty<ColumnMappingSuggestion>(), message);

    private static string? Truncate(string? value) =>
        value is null ? null : (value.Length <= MaxFieldValueLength ? value : value[..MaxFieldValueLength]);

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

    /// <summary>Fully defensive: never throws. Only returns suggestions for columns that were
    /// actually asked about, and only ever a recognized field name (or null) — an AI-hallucinated
    /// field name outside <see cref="ResourceImportRecognizedFields.All"/> is dropped to null
    /// rather than trusted, since the caller applies suggested renames directly onto row data that
    /// later feeds the exact same deterministic import gates every other import path uses.</summary>
    private static IReadOnlyList<ColumnMappingSuggestion>? TryParseOutput(
        string rawResponse, IReadOnlyList<string> requestedColumns, out string? parseError)
    {
        parseError = null;
        var cleaned = CleanJson(rawResponse);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(cleaned);
        }
        catch (JsonException ex)
        {
            parseError = $"Response is not valid JSON: {ex.Message}";
            return null;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("mapping", out var mappingEl)
                || mappingEl.ValueKind != JsonValueKind.Object)
            {
                parseError = "Response is not a JSON object with a 'mapping' property.";
                return null;
            }

            var recognized = new HashSet<string>(ResourceImportRecognizedFields.All, StringComparer.OrdinalIgnoreCase);
            var suggestions = new List<ColumnMappingSuggestion>();

            foreach (var column in requestedColumns)
            {
                if (!mappingEl.TryGetProperty(column, out var entry))
                {
                    suggestions.Add(new ColumnMappingSuggestion(column, null, null));
                    continue;
                }

                string? field = null;
                double? confidence = null;
                if (entry.ValueKind == JsonValueKind.Object)
                {
                    if (entry.TryGetProperty("field", out var fieldEl) && fieldEl.ValueKind == JsonValueKind.String)
                        field = fieldEl.GetString();
                    if (entry.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number
                        && confEl.TryGetDouble(out var c))
                        confidence = Math.Clamp(c, 0d, 1d);
                }
                else if (entry.ValueKind == JsonValueKind.String)
                {
                    field = entry.GetString();
                }

                if (field is not null && !recognized.Contains(field))
                    field = null; // never trust an unrecognized field name from the AI

                suggestions.Add(new ColumnMappingSuggestion(column, field, confidence));
            }

            return suggestions;
        }
    }
}
