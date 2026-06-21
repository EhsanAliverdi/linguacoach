using LinguaCoach.Application.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LinguaCoach.Infrastructure.Ai;

/// <summary>
/// Resolves AI model pricing: active DB override first, appsettings config second, null third.
/// </summary>
public sealed class AiPricingResolver : IAiPricingResolver
{
    private readonly LinguaCoachDbContext _db;
    private readonly IConfiguration _configuration;

    public AiPricingResolver(LinguaCoachDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<ResolvedModelPricing?> ResolveAsync(string providerName, string modelName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(modelName))
            return null;

        var normalizedProvider = providerName.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        // 1. Active DB override: IsActive, EffectiveFromUtc <= now, EffectiveToUtc null or > now
        var dbOverride = await _db.AiModelPricingOverrides
            .Where(o => o.ProviderName == normalizedProvider
                     && o.ModelName == modelName
                     && o.IsActive
                     && o.EffectiveFromUtc <= now
                     && (o.EffectiveToUtc == null || o.EffectiveToUtc > now))
            .OrderByDescending(o => o.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);

        if (dbOverride is not null)
            return new ResolvedModelPricing(dbOverride.InputPricePer1KTokens, dbOverride.OutputPricePer1KTokens);

        // 2. Config fallback — try provider name with original casing first, then lower
        var pricing = AiPricingOptions.GetProviderPricing(_configuration, providerName, modelName)
                   ?? AiPricingOptions.GetProviderPricing(_configuration, NormalizeProviderForConfig(providerName), modelName);

        if (pricing is null) return null;
        return new ResolvedModelPricing(pricing.InputPer1KTokens, pricing.OutputPer1KTokens);
    }

    /// <summary>Maps lowercase provider name to the config section key casing (e.g. "openai" → "OpenAI").</summary>
    private static string NormalizeProviderForConfig(string provider) => provider.ToLowerInvariant() switch
    {
        "openai" => "OpenAI",
        "gemini" => "Gemini",
        "anthropic" => "Anthropic",
        "qwen" => "Qwen",
        _ => provider,
    };
}
