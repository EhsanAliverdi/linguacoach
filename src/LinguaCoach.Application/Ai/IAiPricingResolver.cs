namespace LinguaCoach.Application.Ai;

/// <summary>Resolved pricing for one provider/model pair. InputPer1KCharacters is only ever set for
/// non-token-billed models (TTS) — null for LLM pricing.</summary>
public sealed record ResolvedModelPricing(decimal InputPer1KTokens, decimal OutputPer1KTokens, decimal? InputPer1KCharacters = null);

/// <summary>
/// Resolves AI model pricing. Priority: active DB override → appsettings config → null (zero cost).
/// </summary>
public interface IAiPricingResolver
{
    /// <summary>Returns the effective pricing for the given provider/model, or null if none configured.</summary>
    Task<ResolvedModelPricing?> ResolveAsync(string providerName, string modelName, CancellationToken ct = default);
}
