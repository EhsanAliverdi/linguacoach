using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Resolves TTS service for a feature key.
/// Resolution order:
///   1. AiProviderConfig by exact featureKey (if provider != fake)
///   2. AiConfigCategory by featureKey (tts.listening / tts.placement)
///   3. If provider=fake: return FakeTextToSpeechService (explicit fake = CI/dev intent)
///   4. No config at all: throw AiServiceUnavailableException
/// </summary>
public sealed class TtsProviderResolver
{
    private readonly LinguaCoachDbContext _db;
    private readonly IServiceProvider _services;
    private readonly ILogger<TtsProviderResolver> _logger;

    public TtsProviderResolver(
        LinguaCoachDbContext db,
        IServiceProvider services,
        ILogger<TtsProviderResolver> logger)
    {
        _db = db;
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the TTS service, voice, and model for a feature key.
    /// Throws AiServiceUnavailableException if no configuration exists at all.
    /// Returns FakeTextToSpeechService when provider is explicitly set to "fake".
    /// </summary>
    public async Task<(ITextToSpeechService Service, string? Voice, string? Model)> ResolveAsync(
        string featureKey,
        CancellationToken ct = default)
    {
        // 1. Feature-key-specific AiProviderConfig row
        var featureConfig = await _db.AiProviderConfigs
            .FirstOrDefaultAsync(c => c.FeatureKey == featureKey, ct);

        if (featureConfig is not null)
        {
            return ResolveFromProvider(featureKey, featureConfig.ProviderName, featureConfig.VoiceName, featureConfig.ModelName);
        }

        // 2. AiConfigCategory row (tts.listening / tts.placement map to themselves)
        var categoryConfig = await _db.AiConfigCategories
            .FirstOrDefaultAsync(c => c.CategoryKey == featureKey, ct);

        if (categoryConfig is not null)
        {
            return ResolveFromProvider(featureKey, categoryConfig.ProviderName, categoryConfig.VoiceName, categoryConfig.ModelName);
        }

        // 3. No config at all
        _logger.LogWarning("TtsProviderResolver: no TTS config for feature '{FeatureKey}'", featureKey);
        throw new AiServiceUnavailableException(featureKey);
    }

    private (ITextToSpeechService Service, string? Voice, string? Model) ResolveFromProvider(
        string featureKey, string? providerName, string? voiceName, string? modelName = null)
    {
        var norm = providerName?.ToLowerInvariant();

        switch (norm)
        {
            case "openai":
                _logger.LogDebug("TtsProviderResolver: '{FeatureKey}' → openai voice={Voice}", featureKey, voiceName);
                return (_services.GetRequiredService<OpenAiTextToSpeechService>(), voiceName, modelName);

            case "gemini":
                _logger.LogDebug("TtsProviderResolver: '{FeatureKey}' → gemini voice={Voice}", featureKey, voiceName);
                return (_services.GetRequiredService<GeminiTextToSpeechService>(), voiceName, modelName);

            case "qwen":
                _logger.LogDebug("TtsProviderResolver: '{FeatureKey}' → qwen voice={Voice}", featureKey, voiceName);
                return (_services.GetRequiredService<QwenTextToSpeechService>(), voiceName, modelName);

            case "anthropic":
                // Anthropic does not offer a TTS API — degrade gracefully
                _logger.LogWarning(
                    "TtsProviderResolver: anthropic does not support TTS for '{FeatureKey}' — returning failure service",
                    featureKey);
                return (_services.GetRequiredService<FakeTextToSpeechService>(), voiceName, modelName);

            case "fake":
            case null:
            case "":
                _logger.LogDebug("TtsProviderResolver: '{FeatureKey}' → fake (explicit)", featureKey);
                return (_services.GetRequiredService<FakeTextToSpeechService>(), voiceName, modelName);

            default:
                _logger.LogWarning(
                    "TtsProviderResolver: unknown TTS provider '{Provider}' for '{FeatureKey}', using fake",
                    providerName, featureKey);
                return (_services.GetRequiredService<FakeTextToSpeechService>(), voiceName, modelName);
        }
    }
}
