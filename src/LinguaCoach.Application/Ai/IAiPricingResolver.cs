namespace LinguaCoach.Application.Ai;

/// <summary>Resolved pricing for one provider/model pair.</summary>
public sealed record ResolvedModelPricing(decimal InputPer1KTokens, decimal OutputPer1KTokens);

/// <summary>
/// Resolves AI model pricing. Priority: active DB override → appsettings config → null (zero cost).
/// </summary>
public interface IAiPricingResolver
{
    /// <summary>Returns the effective pricing for the given provider/model, or null if none configured.</summary>
    Task<ResolvedModelPricing?> ResolveAsync(string providerName, string modelName, CancellationToken ct = default);
}
