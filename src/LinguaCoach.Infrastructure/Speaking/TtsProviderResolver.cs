using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

public sealed class TtsProviderResolver
{
    private readonly IAiProviderResolver _resolver;
    private readonly IServiceProvider _services;
    private readonly ILogger<TtsProviderResolver> _logger;

    public TtsProviderResolver(
        IAiProviderResolver resolver,
        IServiceProvider services,
        ILogger<TtsProviderResolver> logger)
    {
        _resolver = resolver;
        _services = services;
        _logger = logger;
    }

    public (ITextToSpeechService Service, TextToSpeechOptions Options) Resolve(
        string featureKey,
        string categoryKey,
        string targetLanguageCode)
    {
        var selection = _resolver.ResolveTts(featureKey, categoryKey);

        ITextToSpeechService service = selection.ProviderName.ToLowerInvariant() switch
        {
            "openai" => _services.GetRequiredService<OpenAiTextToSpeechService>(),
            "gemini" => _services.GetRequiredService<GeminiTextToSpeechService>(),
            "qwen" => _services.GetRequiredService<QwenTextToSpeechService>(),
            "fake" => _services.GetRequiredService<FakeTextToSpeechService>(),
            _ => throw new AiConfigurationUnavailableException(
                $"Unsupported TTS provider '{selection.ProviderName}' for '{featureKey}'.")
        };

        _logger.LogDebug(
            "Resolved TTS FeatureKey={FeatureKey} CategoryKey={CategoryKey} Provider={Provider} Model={Model} Voice={Voice}",
            featureKey, categoryKey, selection.ProviderName, selection.ModelName, selection.VoiceName);

        return (service, new TextToSpeechOptions(
            targetLanguageCode,
            selection.VoiceName,
            selection.ModelName,
            selection.ApiKeyOverride,
            selection.EndpointOverride));
    }
}
