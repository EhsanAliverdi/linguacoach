using LinguaCoach.Application.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Ai;

public sealed class AiProviderResolver : IAiProviderResolver
{
    private const string WritingFeatureKey = "writing.exercise";
    private const string WritingProviderKey = "AI:WritingFeedback:Provider";
    private const string WritingModelKey = "AI:WritingFeedback:Model";
    private const string DefaultDevelopmentProvider = "OpenAI";
    private const string DefaultDevelopmentModel = "gpt-4o-mini";

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiProviderResolver> _logger;

    public AiProviderResolver(
        IConfiguration configuration,
        IHostEnvironment environment,
        IServiceProvider serviceProvider,
        ILogger<AiProviderResolver> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public AiProviderPair ResolveWithFallback(string featureKey)
    {
        var canonicalFeatureKey = CanonicalFeatureKey(featureKey);
        var (providerName, modelName, fallbackProvider, fallbackModel, fallbackEnabled)
            = ResolveFeatureFromDb(canonicalFeatureKey);

        // Fall back to config-based primary if DB returns nothing
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(modelName))
        {
            providerName = _configuration[WritingProviderKey];
            modelName = _configuration[WritingModelKey];
        }

        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(modelName))
        {
            if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
            {
                providerName ??= DefaultDevelopmentProvider;
                modelName ??= DefaultDevelopmentModel;
            }
            else
            {
                throw new AiConfigurationUnavailableException(
                    "AI writing feedback provider and model must be configured.");
            }
        }

        var primaryKey = GetStoredApiKey(providerName) ?? GetEnvApiKey(providerName);
        var primary = Resolve(providerName.Trim(), modelName.Trim(), primaryKey);

        AiProviderSelection? fallback = null;
        if (fallbackEnabled && !string.IsNullOrWhiteSpace(fallbackProvider) && !string.IsNullOrWhiteSpace(fallbackModel))
        {
            try
            {
                var fallbackKey = GetStoredApiKey(fallbackProvider!) ?? GetEnvApiKey(fallbackProvider!);
                fallback = Resolve(fallbackProvider!.Trim(), fallbackModel!.Trim(), fallbackKey);
                _logger.LogDebug("Fallback provider configured Feature={Feature} Fallback={Provider}/{Model}",
                    canonicalFeatureKey, fallbackProvider, fallbackModel);
            }
            catch (AiConfigurationUnavailableException ex)
            {
                _logger.LogWarning("Fallback provider configured but not usable Feature={Feature}: {Message}",
                    canonicalFeatureKey, ex.Message);
            }
        }

        return new AiProviderPair(primary, fallback);
    }

    public AiProviderSelection ResolveWritingFeedbackProvider()
        => ResolveWithFallback(WritingFeatureKey).Primary;

    private (string? Provider, string? Model, string? FallbackProvider, string? FallbackModel, bool FallbackEnabled)
        ResolveFeatureFromDb(string featureKey)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var config = db.AiProviderConfigs.AsNoTracking()
                .FirstOrDefault(c => c.FeatureKey == featureKey);
            if (config is null) return (null, null, null, null, false);
            return (config.ProviderName, config.ModelName,
                    config.FallbackProviderName, config.FallbackModelName, config.FallbackEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read AI feature config from DB for {Feature}.", featureKey);
            return (null, null, null, null, false);
        }
    }

    private static string CanonicalFeatureKey(string featureKey) => featureKey switch
    {
        "activity_generate_writing" => WritingFeatureKey,
        "activity_evaluate_writing" => WritingFeatureKey,
        "writing.exercise.v2" => WritingFeatureKey,
        _ => featureKey
    };

    private string? GetStoredApiKey(string providerName)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var cred = db.AiProviderCredentials.AsNoTracking()
                .FirstOrDefault(c => c.ProviderName == providerName.ToLowerInvariant());
            return cred?.ApiKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read API key from DB for provider {Provider}.", providerName);
            return null;
        }
    }

    private string? GetEnvApiKey(string providerName) =>
        providerName.ToLowerInvariant() switch
        {
            "openai" => _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            "gemini" => _configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            "anthropic" => _configuration["Anthropic:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
            "qwen" => _configuration["Qwen:ApiKey"] ?? Environment.GetEnvironmentVariable("QWEN_API_KEY"),
            _ => null
        };

    private AiProviderSelection Resolve(string provider, string model, string? apiKeyOverride)
    {
        if (string.IsNullOrWhiteSpace(apiKeyOverride))
        {
            throw new AiConfigurationUnavailableException(
                $"{provider} API key is not configured. Set it via the admin UI or the environment variable.");
        }

        return provider.ToLowerInvariant() switch
        {
            "openai" => new AiProviderSelection(
                _serviceProvider.GetRequiredService<OpenAiProvider>(), "openai", model, apiKeyOverride),
            "gemini" => new AiProviderSelection(
                _serviceProvider.GetRequiredService<GeminiProvider>(), "gemini", model, apiKeyOverride),
            "anthropic" => new AiProviderSelection(
                _serviceProvider.GetRequiredService<AnthropicProvider>(), "anthropic", model, apiKeyOverride),
            "qwen" => new AiProviderSelection(
                _serviceProvider.GetRequiredService<QwenProvider>(), "qwen", model, apiKeyOverride),
            _ => throw new AiConfigurationUnavailableException(
                $"Unsupported AI provider '{provider}'. Allowed: OpenAI, Gemini, Anthropic, Qwen.")
        };
    }
}
