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

    public AiProviderSelection ResolveWritingFeedbackProvider()
    {
        // DB config takes precedence over appsettings/env for provider+model selection.
        var (providerName, modelName, apiKeyFromDb) = ResolveFromDb("writing.exercise");

        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(modelName))
        {
            providerName = _configuration[WritingProviderKey];
            modelName = _configuration[WritingModelKey];
        }

        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(modelName))
        {
            if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
            {
                providerName = string.IsNullOrWhiteSpace(providerName) ? DefaultDevelopmentProvider : providerName;
                modelName = string.IsNullOrWhiteSpace(modelName) ? DefaultDevelopmentModel : modelName;
                _logger.LogWarning(
                    "AI writing feedback provider/model not configured. Using default {Provider}/{Model}.",
                    providerName, modelName);
            }
            else
            {
                throw new AiConfigurationUnavailableException(
                    "AI writing feedback provider and model must be configured.");
            }
        }

        return Resolve(providerName.Trim(), modelName.Trim(), apiKeyFromDb);
    }

    private (string? Provider, string? Model, string? ApiKey) ResolveFromDb(string featureKey)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var config = db.AiProviderConfigs
                .AsNoTracking()
                .FirstOrDefault(c => c.FeatureKey == featureKey);
            return config is null
                ? (null, null, null)
                : (config.ProviderName, config.ModelName, config.ApiKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read AI provider config from DB for feature {Feature}; falling back to appsettings.", featureKey);
            return (null, null, null);
        }
    }

    private AiProviderSelection Resolve(string provider, string model, string? apiKeyOverride)
    {
        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var key = apiKeyOverride ?? GetEnvApiKey("OPENAI_API_KEY", "OpenAI:ApiKey", "OpenAI");
            return new AiProviderSelection(_serviceProvider.GetRequiredService<OpenAiProvider>(), "openai", model, key);
        }

        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var key = apiKeyOverride ?? GetEnvApiKey("GEMINI_API_KEY", "Gemini:ApiKey", "Gemini");
            return new AiProviderSelection(_serviceProvider.GetRequiredService<GeminiProvider>(), "gemini", model, key);
        }

        if (provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            var key = apiKeyOverride ?? GetEnvApiKey("ANTHROPIC_API_KEY", "Anthropic:ApiKey", "Anthropic");
            return new AiProviderSelection(_serviceProvider.GetRequiredService<AnthropicProvider>(), "anthropic", model, key);
        }

        throw new AiConfigurationUnavailableException(
            $"Unsupported AI provider '{provider}'. Allowed: OpenAI, Gemini, Anthropic.");
    }

    private string GetEnvApiKey(string environmentVariable, string configurationKey, string providerName)
    {
        var apiKey = _configuration[configurationKey]
            ?? Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiConfigurationUnavailableException(
                $"{providerName} API key is not configured. Set {environmentVariable} or store it via the admin API.");
        }

        return apiKey;
    }
}
