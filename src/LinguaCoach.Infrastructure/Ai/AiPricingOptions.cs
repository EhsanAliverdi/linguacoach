using Microsoft.Extensions.Configuration;

namespace LinguaCoach.Infrastructure.Ai;

public sealed record AiModelPricing(decimal InputPer1KTokens, decimal OutputPer1KTokens);

public static class AiPricingOptions
{
    public static AiModelPricing? GetOpenAiPricing(IConfiguration configuration, string modelName)
        => GetProviderPricing(configuration, "OpenAI", modelName);

    public static AiModelPricing? GetGeminiPricing(IConfiguration configuration, string modelName)
        => GetProviderPricing(configuration, "Gemini", modelName);

    public static AiModelPricing? GetProviderPricing(IConfiguration configuration, string providerName, string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return null;

        var section = configuration.GetSection($"{providerName}:Pricing:{modelName}");
        var input = section.GetValue<decimal?>("InputPer1KTokens");
        var output = section.GetValue<decimal?>("OutputPer1KTokens");

        if (input is null || output is null) return null;
        if (input < 0 || output < 0) return null;

        return new AiModelPricing(input.Value, output.Value);
    }

    /// <summary>Config fallback for non-token-billed models (TTS) — independent of the token-pricing
    /// lookup above, since a TTS model's config section will never have Input/OutputPer1KTokens.</summary>
    public static decimal? GetProviderCharacterPricing(IConfiguration configuration, string providerName, string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return null;

        var section = configuration.GetSection($"{providerName}:Pricing:{modelName}");
        var perCharacter = section.GetValue<decimal?>("PricePer1KCharacters");
        return perCharacter is >= 0 ? perCharacter : null;
    }

    public static decimal EstimateCostUsd(int inputTokens, int outputTokens, AiModelPricing pricing)
    {
        if (inputTokens < 0) throw new ArgumentOutOfRangeException(nameof(inputTokens));
        if (outputTokens < 0) throw new ArgumentOutOfRangeException(nameof(outputTokens));

        return (inputTokens / 1000m) * pricing.InputPer1KTokens
             + (outputTokens / 1000m) * pricing.OutputPer1KTokens;
    }
}
