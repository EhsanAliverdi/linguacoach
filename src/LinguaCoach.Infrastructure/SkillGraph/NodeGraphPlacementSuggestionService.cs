using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>
/// Phase 6.2 (2026-07-23) — AI-proposes candidate prerequisite/dependent edges for a single
/// Skill Graph node. Structurally identical to <see cref="ModuleSkillGraphTaggingService"/>: one
/// bounded AI call, retried once on bad JSON, never throws, and every proposed node key is
/// checked against the real candidate list before being trusted — an AI-hallucinated key is
/// dropped, never applied. Unlike Module tagging, this result is NEVER auto-applied — the caller
/// (AdminSkillGraphController) only returns suggestions for the admin to review, and any accepted
/// suggestion still goes through the existing staged add-prerequisite/add-unlock flow on Edit,
/// only committed on Save.
/// </summary>
public sealed class NodeGraphPlacementSuggestionService : INodeGraphPlacementSuggestionService
{
    public const string SuggestPlacementPromptKey = "skill_graph_suggest_placement";

    private const int MaxCandidateNodes = 60;
    private const int MaxDescriptionLength = 800;

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<NodeGraphPlacementSuggestionService> _logger;

    public NodeGraphPlacementSuggestionService(
        IAiContextBuilder contextBuilder, AiExecutionService aiExecution, ILogger<NodeGraphPlacementSuggestionService> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<NodePlacementSuggestionResult> SuggestPlacementAsync(
        NodePlacementSuggestionRequest request, CancellationToken ct = default)
    {
        if (request.CandidateNodes.Count == 0)
            return new NodePlacementSuggestionResult(true, [], [], null); // nothing to match against — not an error

        var candidates = request.CandidateNodes.Take(MaxCandidateNodes).ToList();
        var byKey = candidates.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

        var variables = new Dictionary<string, string>
        {
            ["nodeTitle"] = request.NodeTitle,
            ["nodeDescription"] = Truncate(request.NodeDescription, MaxDescriptionLength),
            ["cefrLevel"] = request.CefrLevel,
            ["skill"] = request.Skill,
            ["candidateNodesJson"] = JsonSerializer.Serialize(
                candidates.Select(c => new { key = c.Key, title = c.Title })),
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(SuggestPlacementPromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build node placement suggestion prompt (non-blocking).");
            return Failure($"Could not build AI prompt: {ex.Message}");
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(SuggestPlacementPromptKey, aiRequest, null, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider unavailable for node placement suggestion CorrelationId={CorrelationId} (non-blocking).", correlationId);
            return Failure($"AI provider unavailable: {ex.Message}");
        }

        var parsed = TryParseOutput(execResult.ResponseJson, byKey, out var parseError);
        if (parsed is null)
        {
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(SuggestPlacementPromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider unavailable on retry for node placement suggestion CorrelationId={CorrelationId} (non-blocking).", correlationId);
                return Failure($"AI provider unavailable on retry: {ex.Message}");
            }

            parsed = TryParseOutput(execResult.ResponseJson, byKey, out parseError);
            if (parsed is null)
                return Failure($"AI response could not be parsed after retry: {parseError}");
        }

        return new NodePlacementSuggestionResult(true, parsed.Value.Prerequisites, parsed.Value.Dependents, null);
    }

    private static NodePlacementSuggestionResult Failure(string message) => new(false, [], [], message);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

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

    /// <summary>Fully defensive: never throws. Only ever returns node ids for keys present in
    /// <paramref name="byKey"/> — an AI-hallucinated key that doesn't match a real candidate is
    /// dropped, never resolved to a Guid.</summary>
    private static (IReadOnlyList<ModuleSkillGraphNodeMatch> Prerequisites, IReadOnlyList<ModuleSkillGraphNodeMatch> Dependents)? TryParseOutput(
        string rawResponse, IReadOnlyDictionary<string, SkillGraphNodeCandidate> byKey, out string? parseError)
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
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                parseError = "Response is not a JSON object.";
                return null;
            }

            var prerequisites = ParseMatchArray(doc.RootElement, "prerequisites", byKey);
            var dependents = ParseMatchArray(doc.RootElement, "dependents", byKey);
            if (prerequisites is null && dependents is null)
            {
                parseError = "Response has neither a 'prerequisites' nor a 'dependents' array property.";
                return null;
            }

            return (prerequisites ?? [], dependents ?? []);
        }
    }

    private static IReadOnlyList<ModuleSkillGraphNodeMatch>? ParseMatchArray(
        JsonElement root, string propertyName, IReadOnlyDictionary<string, SkillGraphNodeCandidate> byKey)
    {
        if (!root.TryGetProperty(propertyName, out var arrayEl) || arrayEl.ValueKind != JsonValueKind.Array)
            return null;

        var seen = new HashSet<Guid>();
        var results = new List<ModuleSkillGraphNodeMatch>();

        foreach (var match in arrayEl.EnumerateArray())
        {
            if (match.ValueKind != JsonValueKind.Object) continue;
            if (!match.TryGetProperty("key", out var keyEl) || keyEl.ValueKind != JsonValueKind.String) continue;

            var key = keyEl.GetString();
            if (key is null || !byKey.TryGetValue(key, out var candidate)) continue; // never trust an unrecognized key
            if (!seen.Add(candidate.Id)) continue; // drop duplicates

            var confidence = 0.7; // default when the AI omits confidence
            if (match.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number
                && confEl.TryGetDouble(out var c))
                confidence = Math.Clamp(c, 0d, 1d);

            results.Add(new ModuleSkillGraphNodeMatch(candidate.Id, confidence));
        }

        return results;
    }
}
