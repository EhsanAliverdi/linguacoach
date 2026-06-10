using LinguaCoach.Application.Speaking;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Resolves which ITextToSpeechService to use for a given feature key by reading AiProviderConfig.
/// Returns FakeTextToSpeechService when provider=fake (or config missing).
/// Returns OpenAiTextToSpeechService when provider=openai.
/// All other providers fall back to fake.
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
    /// Resolves the TTS service for a feature key and returns the voice to use.
    /// </summary>
    public async Task<(ITextToSpeechService Service, string? Voice)> ResolveAsync(
        string featureKey,
        CancellationToken ct = default)
    {
        var config = await _db.AiProviderConfigs
            .FirstOrDefaultAsync(c => c.FeatureKey == featureKey, ct);

        if (config is null)
        {
            _logger.LogDebug("TtsProviderResolver: no config for feature '{FeatureKey}', using fake", featureKey);
            return (GetFake(), null);
        }

        var providerName = config.ProviderName?.ToLowerInvariant();

        if (providerName == "openai")
        {
            _logger.LogDebug(
                "TtsProviderResolver: feature '{FeatureKey}' → openai voice={Voice}",
                featureKey, config.VoiceName);
            return (_services.GetRequiredService<OpenAiTextToSpeechService>(), config.VoiceName);
        }

        // fake or any unknown provider
        return (GetFake(), config.VoiceName);
    }

    private FakeTextToSpeechService GetFake()
        => _services.GetRequiredService<FakeTextToSpeechService>();
}
