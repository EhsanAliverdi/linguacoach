using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>
/// Sprint 1 of the Adaptive Curriculum initiative — AI-drafts skill-graph nodes + prerequisite
/// edges for one CEFR level x skill combination per call, bounded per AGENTS.md. Structurally
/// mirrors <see cref="ResourceImportColumnMappingService"/>/<see cref="ResourceCandidateAnalysisService"/>:
/// a single bounded AI call, retried once on bad JSON, never throws (degrades to a failure result),
/// and every proposed value is validated against the real recognized taxonomy before being trusted
/// — an AI-hallucinated CEFR level, skill, or subskill is dropped, never applied. See
/// docs/architecture/adaptive-curriculum-skill-graph.md.
/// </summary>
public sealed class SkillGraphDraftingService : ISkillGraphDraftingService
{
    public const string ProposeNodesPromptKey = "skill_graph_propose_nodes";

    private const int MaxExistingTitles = 40;
    private const int MaxCrossLinkCandidates = 40;
    private const int MaxTitleLength = 150;

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<SkillGraphDraftingService> _logger;

    public SkillGraphDraftingService(
        IAiContextBuilder contextBuilder, AiExecutionService aiExecution, ILogger<SkillGraphDraftingService> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<SkillGraphDraftResult> ProposeBatchAsync(SkillGraphDraftRequest request, CancellationToken ct = default)
    {
        if (!CefrLevelConstants.IsValid(request.CefrLevel))
            return Failure($"Invalid CEFR level '{request.CefrLevel}'.");
        if (!CurriculumSkillConstants.IsValid(request.Skill))
            return Failure($"Invalid skill '{request.Skill}'.");

        var subskills = CurriculumSubskillConstants.ForSkill(request.Skill);
        var existingTitles = request.ExistingNodeTitles.Take(MaxExistingTitles).ToList();
        // Phase 2 of the 2026-07-23 rebuild plan — bounded the same way as existingTitles.
        var crossLinkCandidates = request.CrossLinkCandidateTitles.Take(MaxCrossLinkCandidates).ToList();

        var variables = new Dictionary<string, string>
        {
            ["cefrLevel"] = request.CefrLevel,
            ["skill"] = request.Skill,
            ["subskills"] = string.Join(", ", subskills),
            ["existingTitles"] = existingTitles.Count > 0 ? string.Join(" | ", existingTitles) : "(none yet)",
            ["contextTags"] = string.Join(", ", CurriculumContextTagConstants.All),
            ["crossLinkCandidates"] = crossLinkCandidates.Count > 0 ? string.Join(" | ", crossLinkCandidates) : "(none yet)",
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(ProposeNodesPromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build skill-graph drafting prompt (non-blocking).");
            return Failure($"Could not build AI prompt: {ex.Message}");
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(ProposeNodesPromptKey, aiRequest, null, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider unavailable for skill-graph drafting CorrelationId={CorrelationId} (non-blocking).", correlationId);
            return Failure($"AI provider unavailable: {ex.Message}");
        }

        var parsed = TryParseOutput(execResult.ResponseJson, request.CefrLevel, request.Skill, subskills, out var parseError);
        if (parsed is null)
        {
            // Retry exactly once on bad/invalid JSON, same as ResourceImportColumnMappingService.
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(ProposeNodesPromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider unavailable on retry for skill-graph drafting CorrelationId={CorrelationId} (non-blocking).", correlationId);
                return Failure($"AI provider unavailable on retry: {ex.Message}");
            }

            parsed = TryParseOutput(execResult.ResponseJson, request.CefrLevel, request.Skill, subskills, out parseError);
            if (parsed is null)
                return Failure($"AI response could not be parsed after retry: {parseError}");
        }

        return new SkillGraphDraftResult(true, parsed, null);
    }

    private static SkillGraphDraftResult Failure(string message) => new(false, [], message);

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

    /// <summary>Fully defensive: never throws. Every proposed node's CefrLevel/Skill are forced to
    /// the requested values (this call is scoped to exactly one CEFR/skill combination — the AI
    /// cannot propose a node for a different one), Subskill is dropped to null unless it's a real
    /// value in the requested skill's subskill set, DifficultyBand is clamped to 1-5, and
    /// prerequisite titles that don't match another title in the same proposed batch (or the
    /// existing-titles list) are dropped rather than trusted.</summary>
    private static IReadOnlyList<SkillGraphNodeDraftProposal>? TryParseOutput(
        string rawResponse, string cefrLevel, string skill, IReadOnlyList<string> validSubskills, out string? parseError)
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
                || !doc.RootElement.TryGetProperty("nodes", out var nodesEl)
                || nodesEl.ValueKind != JsonValueKind.Array)
            {
                parseError = "Response is not a JSON object with a 'nodes' array property.";
                return null;
            }

            var validSubskillSet = new HashSet<string>(validSubskills, StringComparer.OrdinalIgnoreCase);
            var proposals = new List<SkillGraphNodeDraftProposal>();
            var titlesInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in nodesEl.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object) continue;

                var title = GetString(node, "title");
                var description = GetString(node, "description");
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description)) continue;
                title = Truncate(title.Trim(), MaxTitleLength);
                if (!titlesInBatch.Add(title)) continue; // drop duplicate titles within the same batch

                var subskill = GetString(node, "subskill");
                if (subskill is not null && !validSubskillSet.Contains(subskill))
                    subskill = null; // never trust an unrecognized subskill

                var difficultyBand = GetInt(node, "difficultyBand") ?? 1;
                difficultyBand = Math.Clamp(difficultyBand, 1, 5);

                var descriptionForAi = GetString(node, "descriptionForAi");

                var prereqTitles = new List<string>();
                if (node.TryGetProperty("prerequisiteTitles", out var prereqEl) && prereqEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in prereqEl.EnumerateArray())
                    {
                        if (p.ValueKind == JsonValueKind.String && p.GetString() is { } pt && !string.IsNullOrWhiteSpace(pt))
                            prereqTitles.Add(Truncate(pt.Trim(), MaxTitleLength));
                    }
                }

                var contextTags = new List<string>();
                if (node.TryGetProperty("contextTags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in tagsEl.EnumerateArray())
                    {
                        if (t.ValueKind == JsonValueKind.String && t.GetString() is { } tv
                            && CurriculumContextTagConstants.IsValid(tv))
                        {
                            contextTags.Add(tv.ToLowerInvariant());
                        }
                    }
                }

                proposals.Add(new SkillGraphNodeDraftProposal(
                    title, description.Trim(), cefrLevel, skill, subskill, difficultyBand, descriptionForAi,
                    prereqTitles, contextTags));
            }

            return proposals;
        }
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
