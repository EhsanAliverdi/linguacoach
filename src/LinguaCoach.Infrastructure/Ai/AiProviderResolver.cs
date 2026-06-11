using LinguaCoach.Application.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Ai;

public sealed class AiProviderResolver : IAiProviderResolver
{
    public const string DefaultLlmCategory = "llm.default";

    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiProviderResolver> _logger;

    public AiProviderResolver(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<AiProviderResolver> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public AiProviderPair ResolveLlm(string featureKey, string categoryKey)
    {
        var config = ResolveCategoryConfig(categoryKey, allowDefaultLlm: true);
        if (!IsUsable(config.ProviderName, config.ModelName))
        {
            throw new AiConfigurationUnavailableException(
                $"AI provider is not configured for feature '{featureKey}' in category '{categoryKey}'.");
        }

        var apiKey = GetStoredApiKey(config.ProviderName!) ?? GetEnvApiKey(config.ProviderName!);
        var primary = ResolveLlmProvider(config.ProviderName!, config.ModelName!, apiKey);
        return new AiProviderPair(primary, null);
    }

    public AiTtsProviderSelection ResolveTts(string featureKey, string categoryKey)
    {
        var config = ResolveCategoryConfig(categoryKey, allowDefaultLlm: false);
        if (!IsUsable(config.ProviderName, config.ModelName))
        {
            throw new AiConfigurationUnavailableException(
                $"TTS provider is not configured for feature '{featureKey}' in category '{categoryKey}'.");
        }

        if (string.Equals(config.ProviderName, "anthropic", StringComparison.OrdinalIgnoreCase))
            throw new AiConfigurationUnavailableException("Anthropic does not provide TTS.");

        return new AiTtsProviderSelection(
            config.ProviderName!,
            config.ModelName,
            config.VoiceName,
            GetStoredApiKey(config.ProviderName!) ?? GetEnvApiKey(config.ProviderName!),
            GetStoredEndpoint(config.ProviderName!));
    }

    private (string? ProviderName, string? ModelName, string? VoiceName) ResolveCategoryConfig(
        string categoryKey,
        bool allowDefaultLlm)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            var category = db.AiConfigCategories.AsNoTracking()
                .FirstOrDefault(c => c.CategoryKey == categoryKey);
            if (IsUsable(category?.ProviderName, category?.ModelName))
                return (category!.ProviderName, category.ModelName, category.VoiceName);

            if (allowDefaultLlm && !string.Equals(categoryKey, DefaultLlmCategory, StringComparison.OrdinalIgnoreCase))
            {
                var defaultCategory = db.AiConfigCategories.AsNoTracking()
                    .FirstOrDefault(c => c.CategoryKey == DefaultLlmCategory);
                if (IsUsable(defaultCategory?.ProviderName, defaultCategory?.ModelName))
                    return (defaultCategory!.ProviderName, defaultCategory.ModelName, defaultCategory.VoiceName);
            }

            return (null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read AI category config for {CategoryKey}.", categoryKey);
            return (null, null, null);
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

    private string? GetStoredEndpoint(string providerName)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var cred = db.AiProviderCredentials.AsNoTracking()
                .FirstOrDefault(c => c.ProviderName == providerName.ToLowerInvariant());
            return cred?.ApiEndpoint;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read endpoint from DB for provider {Provider}.", providerName);
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

    private AiProviderSelection ResolveLlmProvider(string provider, string model, string? apiKeyOverride)
    {
        if (string.IsNullOrWhiteSpace(apiKeyOverride))
        {
            throw new AiConfigurationUnavailableException(
                $"{provider} API key is not configured. Set it via the admin UI or an environment variable.");
        }

        var norm = provider.ToLowerInvariant();
        return norm switch
        {
            "openai" => new AiProviderSelection(
                _serviceProvider.GetRequiredService<OpenAiProvider>(), "openai", model, apiKeyOverride),
            "gemini" => new AiProviderSelection(
                _serviceProvider.GetRequiredService<GeminiProvider>(), "gemini", model, apiKeyOverride),
            "anthropic" => new AiProviderSelection(
                _serviceProvider.GetRequiredService<AnthropicProvider>(), "anthropic", model, apiKeyOverride),
            "qwen" => new AiProviderSelection(
                _serviceProvider.GetRequiredService<QwenProvider>(), "qwen", model, apiKeyOverride,
                EndpointOverride: GetStoredEndpoint("qwen") ?? _configuration["Qwen:OpenAiCompatible"]),
            _ => throw new AiConfigurationUnavailableException(
                $"Unsupported AI provider '{provider}'. Allowed: OpenAI, Gemini, Anthropic, Qwen.")
        };
    }

    private static bool IsUsable(string? providerName, string? modelName) =>
        !string.IsNullOrWhiteSpace(providerName)
        && !string.Equals(providerName, "fake", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(modelName)
        && !string.Equals(modelName, "fake", StringComparison.OrdinalIgnoreCase);
}
