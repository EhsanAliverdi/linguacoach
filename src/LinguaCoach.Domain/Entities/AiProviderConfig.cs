using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Maps a feature key (e.g. "writing.exercise", "cefr.assessment", "speaking.turn")
/// to the AI provider and model that should handle it.
/// Stored in PostgreSQL so admins can change the model without a code deploy.
/// </summary>
public sealed class AiProviderConfig : BaseEntity
{
    // Feature key, e.g. "writing.exercise", "cefr.assessment", "speaking.turn".
    public string FeatureKey { get; private set; }

    // Provider name, e.g. "openai", "anthropic".
    public string ProviderName { get; private set; }

    // Model identifier, e.g. "gpt-4o", "gpt-4o-mini", "claude-3-5-sonnet-20241022".
    public string ModelName { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private AiProviderConfig() { FeatureKey = string.Empty; ProviderName = string.Empty; ModelName = string.Empty; }

    public AiProviderConfig(string featureKey, string providerName, string modelName)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) throw new ArgumentException("FeatureKey is required.", nameof(featureKey));
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("ProviderName is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("ModelName is required.", nameof(modelName));

        FeatureKey = featureKey.Trim().ToLowerInvariant();
        ProviderName = providerName.Trim().ToLowerInvariant();
        ModelName = modelName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    // Allowlisted model names. Prevents cost amplification via misconfigured or malicious admin input.
    private static readonly HashSet<string> KnownModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-4",
        "gpt-3.5-turbo", "o1", "o1-mini", "o3-mini",
    };

    public void Update(string providerName, string modelName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("ProviderName is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("ModelName is required.", nameof(modelName));

        var normalised = modelName.Trim();
        if (!KnownModels.Contains(normalised))
            throw new ArgumentException(
                $"Unknown model '{normalised}'. Allowed: {string.Join(", ", KnownModels.Order())}.",
                nameof(modelName));

        ProviderName = providerName.Trim().ToLowerInvariant();
        ModelName = normalised;
        UpdatedAt = DateTime.UtcNow;
    }
}
