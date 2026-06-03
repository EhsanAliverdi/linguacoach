using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Maps a feature key (e.g. "writing.exercise", "cefr.assessment", "speaking.turn")
/// to the AI provider and model that should handle it.
/// Stored in PostgreSQL so admins can change the model without a code deploy.
/// </summary>
public sealed class AiProviderConfig : BaseEntity
{
    public string FeatureKey { get; private set; }
    public string ProviderName { get; private set; }
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

    private static readonly Dictionary<string, HashSet<string>> KnownModelsByProvider = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-4",
            "gpt-3.5-turbo", "o1", "o1-mini", "o3-mini",
        },
        ["gemini"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.5-flash-lite",
            "gemini-2.0-flash", "gemini-2.0-flash-lite",
            "gemini-1.5-pro", "gemini-1.5-flash",
        },
        ["anthropic"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "claude-opus-4-8", "claude-sonnet-4-6", "claude-haiku-4-5",
            "claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022",
            "claude-3-opus-20240229",
        },
    };

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedModels =>
        KnownModelsByProvider.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<string>)kvp.Value);

    public void Update(string providerName, string modelName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("ProviderName is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("ModelName is required.", nameof(modelName));

        var normalisedProvider = providerName.Trim().ToLowerInvariant();
        if (!KnownModelsByProvider.TryGetValue(normalisedProvider, out var allowedModels))
            throw new ArgumentException(
                $"Unsupported provider '{normalisedProvider}'. Allowed: {string.Join(", ", KnownModelsByProvider.Keys)}.",
                nameof(providerName));

        var normalised = modelName.Trim();
        if (!allowedModels.Contains(normalised))
            throw new ArgumentException(
                $"Unknown model '{normalised}' for provider '{normalisedProvider}'. Allowed: {string.Join(", ", allowedModels.Order())}.",
                nameof(modelName));

        ProviderName = normalisedProvider;
        ModelName = normalised;
        UpdatedAt = DateTime.UtcNow;
    }

}
