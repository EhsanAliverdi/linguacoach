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
        // DB config (feature routing) takes precedence over appsettings.
        var (providerName, modelName) = ResolveFeatureFromDb("writing.exercise");

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

        // Resolve API key from the per-provider credential store, then fall back to env.
        var apiKey = GetStoredApiKey(providerName) ?? GetEnvApiKey(providerName);
        return Resolve(providerName.Trim(), modelName.Trim(), apiKey);
    }

    private (string? Provider, string? Model) ResolveFeatureFromDb(string featureKey)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var config = db.AiProviderConfigs.AsNoTracking()
                .FirstOrDefault(c => c.FeatureKey == featureKey);
            return config is null ? (null, null) : (config.ProviderName, config.ModelName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read AI feature config from DB for {Feature}.", featureKey);
            return (null, null);
        }
    }

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
            _ => throw new AiConfigurationUnavailableException(
                $"Unsupported AI provider '{provider}'. Allowed: OpenAI, Gemini, Anthropic.")
        };
    }
}
