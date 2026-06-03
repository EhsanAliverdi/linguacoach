namespace LinguaCoach.Application.Ai;

/// <summary>
/// Sends a minimal prompt to verify that a provider's API key and connectivity work.
/// Returns (ok, latencyMs, error).
/// </summary>
public interface IAiProviderTester
{
    Task<(bool Ok, int LatencyMs, string? Error)> TestAsync(
        string providerName,
        string? apiKeyOverride,
        CancellationToken ct = default);
}
