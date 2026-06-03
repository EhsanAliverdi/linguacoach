namespace LinguaCoach.Application.Ai;

public sealed record ModelTestOutcome(string ModelName, bool Ok, int LatencyMs, string? Error);

/// <summary>
/// Tests each model for a provider by sending a minimal prompt.
/// Returns one result per model.
/// </summary>
public interface IAiProviderTester
{
    Task<IReadOnlyList<ModelTestOutcome>> TestAllModelsAsync(
        string providerName,
        IEnumerable<string> models,
        string? apiKeyOverride,
        CancellationToken ct = default);
}
