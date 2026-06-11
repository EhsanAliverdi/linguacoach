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
    public string? VoiceName { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AiProviderConfig() { FeatureKey = string.Empty; ProviderName = string.Empty; ModelName = string.Empty; }

    public AiProviderConfig(string featureKey, string providerName, string modelName, string? voiceName = null)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) throw new ArgumentException("FeatureKey is required.", nameof(featureKey));
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("ProviderName is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("ModelName is required.", nameof(modelName));

        FeatureKey = featureKey.Trim().ToLowerInvariant();
        ProviderName = providerName.Trim().ToLowerInvariant();
        ModelName = modelName.Trim();
        VoiceName = string.IsNullOrWhiteSpace(voiceName) ? null : voiceName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    // Fallback provider — null means no fallback configured
    public string? FallbackProviderName { get; private set; }
    public string? FallbackModelName { get; private set; }
    public bool FallbackEnabled { get; private set; }

    public void SetFallback(string? providerName, string? modelName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(modelName))
        {
            FallbackProviderName = null;
            FallbackModelName = null;
            FallbackEnabled = false;
            UpdatedAt = DateTime.UtcNow;
            return;
        }

        var normProvider = providerName.Trim().ToLowerInvariant();
        if (!KnownModelsByProvider.TryGetValue(normProvider, out var allowedModels))
            throw new ArgumentException(
                $"Unsupported fallback provider '{normProvider}'.", nameof(providerName));

        var normModel = modelName.Trim();
        if (!allowedModels.Contains(normModel))
            throw new ArgumentException(
                $"Unknown model '{normModel}' for fallback provider '{normProvider}'.", nameof(modelName));

        FallbackProviderName = normProvider;
        FallbackModelName = normModel;
        FallbackEnabled = enabled;
        UpdatedAt = DateTime.UtcNow;
    }

    private static readonly Dictionary<string, HashSet<string>> KnownModelsByProvider = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-4",
            "gpt-3.5-turbo", "o1", "o1-mini", "o3-mini",
            // TTS models
            "tts-1", "tts-1-hd",
        },
        ["fake"] = new(StringComparer.OrdinalIgnoreCase) { "fake" },
        ["gemini"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.5-flash-lite",
            // TTS model
            "gemini-3.1-flash-tts-preview",
            "gemini-2.5-flash-preview-tts", "gemini-2.5-pro-preview-tts",
        },
        ["anthropic"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "claude-opus-4-8", "claude-sonnet-4-6", "claude-haiku-4-5",
            "claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022",
            "claude-3-opus-20240229",
        },
        ["qwen"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "qwen-plus", "qwen-max", "qwen-turbo", "qwen3-235b-a22b", "qwen3-coder-plus",
            // TTS model
            "cosyvoice-v2",
        },
    };

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedModels =>
        KnownModelsByProvider.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<string>)kvp.Value);

    public void UpdateVoice(string? voiceName)
    {
        VoiceName = string.IsNullOrWhiteSpace(voiceName) ? null : voiceName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

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
