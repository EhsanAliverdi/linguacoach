using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Composer;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Composer;

/// <summary>
/// Adaptive Curriculum Sprint 5 — see <see cref="ICurriculumComposerService"/> for the full
/// contract description. Structurally identical to <c>ModuleSkillGraphTaggingService</c>: one
/// bounded AI call, retried once on bad JSON, never throws, every ranked id validated against the
/// real candidate set given in the request before being trusted.
/// </summary>
public sealed class CurriculumComposerService : ICurriculumComposerService
{
    public const string RankCandidatesPromptKey = "curriculum_composer_rank_candidates";

    private const int MaxCandidatesInPrompt = 40;

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<CurriculumComposerService> _logger;

    public CurriculumComposerService(
        IAiContextBuilder contextBuilder, AiExecutionService aiExecution, ILogger<CurriculumComposerService> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<ComposerRankingResult> RankCandidatesAsync(
        ComposerRankingRequest request, CancellationToken ct = default)
    {
        if (request.Candidates.Count == 0)
            return Failure("No eligible candidates were provided to rank.");

        var maxResults = Math.Max(1, request.MaxResults);
        var candidates = request.Candidates.Take(MaxCandidatesInPrompt).ToList();
        var byId = candidates.ToDictionary(c => c.ModuleId);

        var variables = new Dictionary<string, string>
        {
            ["surfaceName"] = request.SurfaceName,
            ["maxResults"] = maxResults.ToString(),
            ["requestedSkill"] = request.RequestedSkill ?? "(none — no specific skill requested)",
            ["requestedSubskill"] = request.RequestedSubskill ?? "(none)",
            ["requestedObjectiveKey"] = request.RequestedObjectiveKey ?? "(none)",
            ["preferredSessionLengthMinutes"] = request.PreferredSessionLengthMinutes?.ToString() ?? "(no preference given)",
            ["requestedDifficulty"] = request.RequestedDifficulty?.ToString() ?? "(no preference given)",
            ["candidatesJson"] = JsonSerializer.Serialize(candidates.Select(c => new
            {
                id = c.ModuleId,
                title = c.Title,
                skill = c.Skill,
                subskill = c.Subskill,
                cefrLevel = c.CefrLevel,
                difficultyBand = c.DifficultyBand,
                estimatedMinutes = c.EstimatedMinutes,
                contextTags = c.ContextTags,
                focusTags = c.FocusTags,
                objectiveKey = c.ObjectiveKey,
                isWeaknessMatch = c.IsWeaknessMatch,
                isGoalMatch = c.IsGoalMatch,
                recentlyPractisedSameSkill = c.RecentlyPractisedSameSkill
            }))
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(RankCandidatesPromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build curriculum composer prompt (non-blocking).");
            return Failure($"Could not build AI prompt: {ex.Message}");
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(RankCandidatesPromptKey, aiRequest, request.StudentId, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider unavailable for curriculum composer CorrelationId={CorrelationId} (non-blocking).", correlationId);
            return Failure($"AI provider unavailable: {ex.Message}");
        }

        var parsed = TryParseOutput(execResult.ResponseJson, byId, maxResults, out var reason, out var parseError);
        if (parsed is null)
        {
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(RankCandidatesPromptKey, aiRequest, request.StudentId, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider unavailable on retry for curriculum composer CorrelationId={CorrelationId} (non-blocking).", correlationId);
                return Failure($"AI provider unavailable on retry: {ex.Message}");
            }

            parsed = TryParseOutput(execResult.ResponseJson, byId, maxResults, out reason, out parseError);
            if (parsed is null)
                return Failure($"AI response could not be parsed after retry: {parseError}");
        }

        if (parsed.Count == 0)
            return Failure("AI ranking produced no recognized candidate id.");

        return new ComposerRankingResult(true, parsed, reason, null);
    }

    private static ComposerRankingResult Failure(string message) => new(false, [], null, message);

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

    /// <summary>Fully defensive: never throws. Only ever returns ids present in <paramref name="byId"/>
    /// — an AI-hallucinated id is dropped, never surfaced to a caller.</summary>
    private static IReadOnlyList<Guid>? TryParseOutput(
        string rawResponse,
        IReadOnlyDictionary<Guid, ComposerCandidate> byId,
        int maxResults,
        out string? reason,
        out string? parseError)
    {
        reason = null;
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
                || !doc.RootElement.TryGetProperty("rankedModuleIds", out var rankedEl)
                || rankedEl.ValueKind != JsonValueKind.Array)
            {
                parseError = "Response is not a JSON object with a 'rankedModuleIds' array property.";
                return null;
            }

            if (doc.RootElement.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String)
                reason = Truncate(reasonEl.GetString(), 500);

            var seen = new HashSet<Guid>();
            var results = new List<Guid>();

            foreach (var idEl in rankedEl.EnumerateArray())
            {
                if (results.Count >= maxResults) break;
                if (idEl.ValueKind != JsonValueKind.String) continue;
                if (!Guid.TryParse(idEl.GetString(), out var id)) continue;
                if (!byId.ContainsKey(id)) continue; // never trust an unrecognized id
                if (!seen.Add(id)) continue; // drop duplicates

                results.Add(id);
            }

            return results;
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
