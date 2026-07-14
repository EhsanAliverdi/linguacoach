using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Ai;

namespace LinguaCoach.Infrastructure.AdminRepair;

/// <summary>
/// Phase K8 — the one shared AI call every entity-specific repair service (Resource Bank/Lesson/
/// Exercise/Module) uses to fill in a single missing text field. Deliberately generic and
/// single-purpose — "given this context, write this one field" — so a single prompt template
/// (<see cref="PromptKey"/>) serves all four admin pages instead of one prompt per entity type.
/// Never used for correctness-critical data (answer keys, scoring rules, Form.io schemas) — only
/// for descriptive/explanatory text, mirroring the same policy
/// <see cref="Exercises.AiExerciseGenerationService"/> already applies.
/// </summary>
public sealed class AdminRepairFieldGenerator
{
    public const string PromptKey = "admin_content_repair_field";

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;

    public AdminRepairFieldGenerator(IAiContextBuilder contextBuilder, AiExecutionService aiExecution)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
    }

    public sealed record GeneratedField(string Value, string ProviderName, string ModelName);

    /// <param name="entityKind">Human-readable kind, e.g. "Vocabulary resource", "Lesson".</param>
    /// <param name="fieldLabel">What the missing field is, e.g. "a concise definition (1 sentence)".</param>
    /// <param name="context">Grounding text — existing known fields (title/word/CEFR level/etc.),
    /// so the AI never invents context it wasn't given.</param>
    public async Task<GeneratedField> GenerateFieldAsync(
        string entityKind, string fieldLabel, string context, CancellationToken ct)
    {
        var variables = new Dictionary<string, string>
        {
            ["entityKind"] = entityKind,
            ["fieldLabel"] = fieldLabel,
            ["context"] = context,
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        var request = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
        var result = await _aiExecution.ExecuteWithMetaAsync(PromptKey, request, null, correlationId, ct);
        var value = ParseValue(result.ResponseJson);
        return new GeneratedField(value, result.ProviderName, result.ModelName);
    }

    private static string ParseValue(string rawResponse)
    {
        var cleaned = CleanJson(rawResponse);
        using var doc = JsonDocument.Parse(cleaned);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("value", out var el)
            || el.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(el.GetString()))
        {
            throw new InvalidOperationException("AI repair response did not contain a non-empty 'value' string.");
        }
        return el.GetString()!.Trim();
    }

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
}
