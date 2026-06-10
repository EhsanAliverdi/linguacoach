using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
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

        // If the feature-specific row is absent or fake, try category → llm.default
        if (string.IsNullOrWhiteSpace(providerName)
            || string.Equals(providerName, "fake", StringComparison.OrdinalIgnoreCase))
        {
            var (catProvider, catModel) = ResolveCategoryFromDb(canonicalFeatureKey);
            if (!string.IsNullOrWhiteSpace(catProvider) && !string.Equals(catProvider, "fake", StringComparison.OrdinalIgnoreCase))
            {
                providerName = catProvider;
                modelName = catModel;
                fallbackProvider = null;
                fallbackModel = null;
                fallbackEnabled = false;
            }
        }

        // Fall back to legacy appsettings-based config
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
                    $"AI provider is not configured for feature '{canonicalFeatureKey}'. " +
                    "Set a default provider in Admin → AI Configuration.");
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

    // Maps feature keys to their AI config category for category-level resolution.
    private static readonly Dictionary<string, string> FeatureToCategory =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["activity_generate_writing"]                     = "llm.generation",
            ["activity_generate_listening"]                   = "llm.generation",
            ["activity_generate_speaking_roleplay"]           = "llm.generation",
            ["activity_generate_phrase_match"]                = "llm.generation",
            ["activity_generate_gap_fill_workplace_phrase"]   = "llm.generation",
            ["activity_generate_listen_and_answer"]           = "llm.generation",
            ["activity_generate_listen_and_gap_fill"]         = "llm.generation",
            ["activity_generate_email_reply"]                 = "llm.generation",
            ["activity_generate_teams_chat_simulation"]       = "llm.generation",
            ["activity_generate_spoken_response_from_prompt"] = "llm.generation",
            ["activity_generate_lesson_reflection"]           = "llm.generation",
            ["activity_evaluate_writing"]                     = "llm.evaluation",
            ["activity_evaluate_speaking_roleplay"]           = "llm.evaluation",
            ["activity_evaluate_phrase_match"]                = "llm.evaluation",
            ["activity_evaluate_gap_fill_workplace_phrase"]   = "llm.evaluation",
            ["activity_evaluate_listen_and_answer"]           = "llm.evaluation",
            ["activity_evaluate_listen_and_gap_fill"]         = "llm.evaluation",
            ["activity_evaluate_email_reply"]                 = "llm.evaluation",
            ["activity_evaluate_teams_chat_simulation"]       = "llm.evaluation",
            ["activity_evaluate_spoken_response_from_prompt"] = "llm.evaluation",
            ["activity_evaluate_lesson_reflection"]           = "llm.evaluation",
            ["writing.exercise"]                              = "llm.evaluation",
            ["placement_assessment_evaluate"]                 = "llm.evaluation",
            ["learning_path_generate"]                        = "llm.memory",
            ["learning_path_generate_adaptive"]               = "llm.memory",
            ["student_memory_update"]                         = "llm.memory",
            ["vocabulary_extract_from_attempt"]               = "llm.memory",
        };

    private (string? Provider, string? Model) ResolveCategoryFromDb(string featureKey)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            // Try category-specific row first
            if (FeatureToCategory.TryGetValue(featureKey, out var categoryKey))
            {
                var cat = db.AiConfigCategories.AsNoTracking()
                    .FirstOrDefault(c => c.CategoryKey == categoryKey);
                if (cat?.IsConfigured == true)
                    return (cat.ProviderName, cat.ModelName);
            }

            // Fall through to llm.default (TTS keys never reach here — they use TtsProviderResolver)
            var def = db.AiConfigCategories.AsNoTracking()
                .FirstOrDefault(c => c.CategoryKey == "llm.default");
            if (def?.IsConfigured == true)
                return (def.ProviderName, def.ModelName);

            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read AI category config from DB for {Feature}.", featureKey);
            return (null, null);
        }
    }

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
