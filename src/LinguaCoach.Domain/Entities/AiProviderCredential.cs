using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

public sealed record ModelTestResult(bool Ok, int LatencyMs, string? Error, DateTime TestedAt);

/// <summary>
/// Stores the API key and per-model test results for one AI provider.
/// One row per provider — shared across all features using that provider.
/// </summary>
public sealed class AiProviderCredential : BaseEntity
{
    public string ProviderName { get; private set; }
    public string? ApiKey { get; private set; }
    /// <summary>Optional custom API endpoint/base URL (used by Qwen workspace endpoints).</summary>
    public string? ApiEndpoint { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Per-model test results stored as JSON. Key = model name.
    public Dictionary<string, ModelTestResult> ModelTests { get; private set; } = new();

    private static readonly HashSet<string> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
        { "openai", "gemini", "anthropic", "qwen" };

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
        // Clear stale test results when the key changes.
        ModelTests = new();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetApiEndpoint(string? endpoint)
    {
        ApiEndpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();
        ModelTests = new();
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordModelTest(string modelName, bool ok, int latencyMs, string? error)
    {
        ModelTests[modelName] = new ModelTestResult(ok, latencyMs, error, DateTime.UtcNow);
        UpdatedAt = DateTime.UtcNow;
    }
}
