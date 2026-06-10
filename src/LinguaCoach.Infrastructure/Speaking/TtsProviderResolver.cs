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
    /// Resolves the TTS service and voice for a feature key.
    /// Throws AiServiceUnavailableException if no configuration exists at all.
    /// Returns FakeTextToSpeechService when provider is explicitly set to "fake".
    /// </summary>
    public async Task<(ITextToSpeechService Service, string? Voice)> ResolveAsync(
        string featureKey,
        CancellationToken ct = default)
    {
        // 1. Feature-key-specific AiProviderConfig row
        var featureConfig = await _db.AiProviderConfigs
            .FirstOrDefaultAsync(c => c.FeatureKey == featureKey, ct);

        if (featureConfig is not null)
        {
            return ResolveFromProvider(featureKey, featureConfig.ProviderName, featureConfig.VoiceName);
        }

        // 2. AiConfigCategory row (tts.listening / tts.placement map to themselves)
        var categoryConfig = await _db.AiConfigCategories
            .FirstOrDefaultAsync(c => c.CategoryKey == featureKey, ct);

        if (categoryConfig is not null)
        {
            return ResolveFromProvider(featureKey, categoryConfig.ProviderName, categoryConfig.VoiceName);
        }

        // 3. No config at all
        _logger.LogWarning("TtsProviderResolver: no TTS config for feature '{FeatureKey}'", featureKey);
        throw new AiServiceUnavailableException(featureKey);
    }

    private (ITextToSpeechService Service, string? Voice) ResolveFromProvider(
        string featureKey, string? providerName, string? voiceName)
    {
        var norm = providerName?.ToLowerInvariant();

        if (norm == "openai")
        {
            _logger.LogDebug("TtsProviderResolver: '{FeatureKey}' → openai voice={Voice}", featureKey, voiceName);
            return (_services.GetRequiredService<OpenAiTextToSpeechService>(), voiceName);
        }

        if (norm == "fake" || string.IsNullOrWhiteSpace(norm))
        {
            _logger.LogDebug("TtsProviderResolver: '{FeatureKey}' → fake (explicit)", featureKey);
            return (_services.GetRequiredService<FakeTextToSpeechService>(), voiceName);
        }

        // Unknown provider — treat as fake in dev, throw in prod
        _logger.LogWarning("TtsProviderResolver: unknown TTS provider '{Provider}' for '{FeatureKey}', using fake",
            providerName, featureKey);
        return (_services.GetRequiredService<FakeTextToSpeechService>(), voiceName);
    }
}
