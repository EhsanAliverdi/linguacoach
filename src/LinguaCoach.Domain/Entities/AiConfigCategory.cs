using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Category-level AI provider configuration.
/// One row per category (llm.default, llm.generation, llm.evaluation, llm.memory, tts.listening, tts.placement).
/// Feature-key-level AiProviderConfig rows override these when present.
/// </summary>
public sealed class AiConfigCategory : BaseEntity
{
    public string CategoryKey { get; private set; }
    public string DisplayName { get; private set; }
    public string? ProviderName { get; private set; }
    public string? ModelName { get; private set; }
    public string? VoiceName { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AiConfigCategory() { CategoryKey = string.Empty; DisplayName = string.Empty; }

    public AiConfigCategory(string categoryKey, string displayName, string? providerName = null, string? modelName = null, string? voiceName = null)
    {
        if (string.IsNullOrWhiteSpace(categoryKey)) throw new ArgumentException("CategoryKey is required.", nameof(categoryKey));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("DisplayName is required.", nameof(displayName));

        CategoryKey = categoryKey.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        ProviderName = NullOrNorm(providerName);
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName.Trim();
        VoiceName = NullOrNorm(voiceName);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? providerName, string? modelName)
    {
        ProviderName = NullOrNorm(providerName);
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateVoice(string? voiceName)
    {
        VoiceName = NullOrNorm(voiceName);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>True when a real (non-fake) provider is configured.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProviderName)
        && !string.Equals(ProviderName, "fake", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(ModelName)
        && !string.Equals(ModelName, "fake", StringComparison.OrdinalIgnoreCase);

    private static string? NullOrNorm(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToLowerInvariant();
}
