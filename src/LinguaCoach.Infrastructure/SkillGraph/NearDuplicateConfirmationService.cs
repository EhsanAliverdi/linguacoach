using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>
/// Phase 6.3f (2026-07-24) — on-demand AI second opinion for one candidate near-duplicate Skill
/// Graph node pair, requested explicitly per-pair by the admin from the near-duplicate audit
/// results (never run automatically during that deterministic audit, to keep it cheap and keep AI
/// cost strictly opt-in). Structurally identical to <see cref="NodeGraphPlacementSuggestionService"/>:
/// one bounded AI call, retried once on bad JSON, never throws. Purely advisory — the admin still
/// decides whether to merge; this service never merges or dismisses anything itself.
/// </summary>
public sealed class NearDuplicateConfirmationService : INearDuplicateConfirmationService
{
    public const string ConfirmNearDuplicatePromptKey = "skill_graph_confirm_near_duplicate";

    private const int MaxDescriptionLength = 800;

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<NearDuplicateConfirmationService> _logger;

    public NearDuplicateConfirmationService(
        IAiContextBuilder contextBuilder, AiExecutionService aiExecution, ILogger<NearDuplicateConfirmationService> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<NearDuplicateConfirmationResult> ConfirmAsync(
        NearDuplicateConfirmationRequest request, CancellationToken ct = default)
    {
        var variables = new Dictionary<string, string>
        {
            ["nodeATitle"] = request.NodeATitle,
            ["nodeADescription"] = Truncate(request.NodeADescription, MaxDescriptionLength),
            ["nodeBTitle"] = request.NodeBTitle,
            ["nodeBDescription"] = Truncate(request.NodeBDescription, MaxDescriptionLength),
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(ConfirmNearDuplicatePromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build near-duplicate confirmation prompt (non-blocking).");
            return Failure($"Could not build AI prompt: {ex.Message}");
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(ConfirmNearDuplicatePromptKey, aiRequest, null, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider unavailable for near-duplicate confirmation CorrelationId={CorrelationId} (non-blocking).", correlationId);
            return Failure($"AI provider unavailable: {ex.Message}");
        }

        var parsed = TryParseOutput(execResult.ResponseJson, out var parseError);
        if (parsed is null)
        {
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(ConfirmNearDuplicatePromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider unavailable on retry for near-duplicate confirmation CorrelationId={CorrelationId} (non-blocking).", correlationId);
                return Failure($"AI provider unavailable on retry: {ex.Message}");
            }

            parsed = TryParseOutput(execResult.ResponseJson, out parseError);
            if (parsed is null)
                return Failure($"AI response could not be parsed after retry: {parseError}");
        }

        return new NearDuplicateConfirmationResult(true, parsed.Value.IsDuplicate, parsed.Value.Reasoning, null);
    }

    private static NearDuplicateConfirmationResult Failure(string message) => new(false, null, null, message);

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

    private static (bool IsDuplicate, string Reasoning)? TryParseOutput(string rawResponse, out string? parseError)
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

            if (!doc.RootElement.TryGetProperty("isDuplicate", out var isDuplicateEl) ||
                isDuplicateEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                parseError = "Response is missing a boolean 'isDuplicate' property.";
                return null;
            }

            var reasoning = doc.RootElement.TryGetProperty("reasoning", out var reasoningEl) && reasoningEl.ValueKind == JsonValueKind.String
                ? reasoningEl.GetString() ?? ""
                : "";

            return (isDuplicateEl.GetBoolean(), reasoning);
        }
    }
}
