using LinguaCoach.Application.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Ai;

/// <summary>
/// Fetches the active prompt template from the database by key, substitutes
/// {{variable}} placeholders with caller-supplied values, and enforces the
/// per-feature token budget stored on the template record.
///
/// Token counting uses a rough character-based estimate (4 chars ≈ 1 token).
/// A real tokeniser is not required at MVP scale.
/// </summary>
public sealed class DbPromptAiContextBuilder : IAiContextBuilder
{
    private readonly LinguaCoachDbContext _db;

    public DbPromptAiContextBuilder(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AiRequest> BuildAsync(
        string promptKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default)
    {
        var template = await _db.AiPrompts
            .Where(p => p.Key == promptKey && p.IsActive)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct)
            ?? throw new AiConfigurationUnavailableException(
                "Writing feedback is temporarily unavailable because the AI prompt is not configured yet.");

        var rendered = Render(template.Content, variables);
        rendered = InsertLearnerPreferenceContext(rendered, variables);
        rendered = InsertRoutingContext(rendered, variables);

        if (template.MaxInputTokens.HasValue)
        {
            var estimated = EstimateTokens(rendered);
            if (estimated > template.MaxInputTokens.Value)
                throw new TokenBudgetExceededException(promptKey, estimated, template.MaxInputTokens.Value);
        }

        // Look up configured model for this feature (strip version suffix).
        var featureKey = DeriveFeatureKey(promptKey);
        var providerConfig = await _db.AiProviderConfigs
            .FirstOrDefaultAsync(c => c.FeatureKey == featureKey, ct);

        return new AiRequest(
            PromptKey: promptKey,
            RenderedPrompt: rendered,
            MaxOutputTokens: template.MaxOutputTokens ?? 800,
            ModelHint: providerConfig?.ModelName ?? string.Empty);
    }

    private static string DeriveFeatureKey(string promptKey)
    {
        // "writing.exercise.v1" → "writing.exercise", "speaking.turn.v1" → "speaking.turn"
        var parts = promptKey.Split('.');
        return parts.Length >= 3 && parts[^1].StartsWith('v') && int.TryParse(parts[^1][1..], out _)
            ? string.Join('.', parts[..^1])
            : promptKey;
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
            result = result.Replace($"{{{{{key}}}}}", value ?? string.Empty);
        return result;
    }

    private static string InsertLearnerPreferenceContext(
        string rendered,
        IReadOnlyDictionary<string, string> variables)
    {
        if (!variables.TryGetValue("learnerPreferences", out var learnerPreferences)
            || string.IsNullOrWhiteSpace(learnerPreferences)
            || rendered.Contains(learnerPreferences, StringComparison.Ordinal))
            return rendered;

        var section = $"{learnerPreferences.Trim()}{Environment.NewLine}{Environment.NewLine}" +
            "Preference behaviour rules:" + Environment.NewLine +
            "- Let goals and focus areas guide topic emphasis." + Environment.NewLine +
            "- Use support language only as optional help." + Environment.NewLine +
            "- Do not translate the whole activity by default." + Environment.NewLine +
            "- Do not assume workplace context unless requested." + Environment.NewLine + Environment.NewLine;

        var returnOnlyIndex = rendered.IndexOf("Return ONLY", StringComparison.OrdinalIgnoreCase);
        if (returnOnlyIndex >= 0)
            return rendered.Insert(returnOnlyIndex, section);

        return $"{rendered.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{section.TrimEnd()}";
    }

    private static string InsertRoutingContext(
        string rendered,
        IReadOnlyDictionary<string, string> variables)
    {
        if (!variables.TryGetValue("routingContext", out var routingContext)
            || string.IsNullOrWhiteSpace(routingContext)
            || rendered.Contains(routingContext, StringComparison.Ordinal))
            return rendered;

        var section = $"Curriculum context: {routingContext.Trim()}{Environment.NewLine}{Environment.NewLine}";

        var returnOnlyIndex = rendered.IndexOf("Return ONLY", StringComparison.OrdinalIgnoreCase);
        if (returnOnlyIndex >= 0)
            return rendered.Insert(returnOnlyIndex, section);

        return $"{rendered.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{section.TrimEnd()}";
    }

    // Rough estimate: 4 characters ≈ 1 token (GPT-4 tokeniser average).
    private static int EstimateTokens(string text) => (text.Length + 3) / 4;
}
