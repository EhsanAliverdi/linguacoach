using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Stores the API key and last-tested status for one AI provider (openai, gemini, anthropic).
/// One row per provider — shared across all features that use that provider.
/// Null ApiKey means "fall back to the environment variable".
/// </summary>
public sealed class AiProviderCredential : BaseEntity
{
    public string ProviderName { get; private set; }
    public string? ApiKey { get; private set; }
    public bool LastTestOk { get; private set; }
    public DateTime? LastTestedAt { get; private set; }
    public string? LastTestError { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private static readonly HashSet<string> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
        { "openai", "gemini", "anthropic" };

    private AiProviderCredential() { ProviderName = string.Empty; }

    public AiProviderCredential(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("ProviderName is required.", nameof(providerName));

        var normalised = providerName.Trim().ToLowerInvariant();
        if (!KnownProviders.Contains(normalised))
            throw new ArgumentException($"Unknown provider '{normalised}'.", nameof(providerName));

        ProviderName = normalised;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetApiKey(string? apiKey)
    {
        ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordTestResult(bool ok, string? error = null)
    {
        LastTestOk = ok;
        LastTestedAt = DateTime.UtcNow;
        LastTestError = ok ? null : error;
        UpdatedAt = DateTime.UtcNow;
    }
}
