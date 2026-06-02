using LinguaCoach.Application.Ai;
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
        var providerName = _configuration[WritingProviderKey];
        var modelName = _configuration[WritingModelKey];

        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(modelName))
        {
            if (_environment.IsDevelopment())
            {
                providerName = string.IsNullOrWhiteSpace(providerName) ? DefaultDevelopmentProvider : providerName;
                modelName = string.IsNullOrWhiteSpace(modelName) ? DefaultDevelopmentModel : modelName;
                _logger.LogWarning(
                    "AI writing feedback provider/model not fully configured. Using Development default {Provider}/{Model}.",
                    providerName,
                    modelName);
            }
            else
            {
                throw new AiConfigurationUnavailableException(
                    "AI writing feedback provider and model must be configured with AI:WritingFeedback:Provider and AI:WritingFeedback:Model.");
            }
        }

        var provider = providerName.Trim();
        var model = modelName.Trim();

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            RequireApiKey("OPENAI_API_KEY", "OpenAI:ApiKey", "OpenAI");
            return new AiProviderSelection(
                _serviceProvider.GetRequiredService<OpenAiProvider>(),
                "openai",
                model);
        }

        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            RequireApiKey("GEMINI_API_KEY", "Gemini:ApiKey", "Gemini");
            return new AiProviderSelection(
                _serviceProvider.GetRequiredService<GeminiProvider>(),
                "gemini",
                model);
        }

        throw new AiConfigurationUnavailableException(
            $"Unsupported AI writing feedback provider '{provider}'. Allowed providers: OpenAI, Gemini.");
    }

    private void RequireApiKey(string environmentVariable, string configurationKey, string providerName)
    {
        var apiKey = _configuration[configurationKey]
            ?? Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiConfigurationUnavailableException(
                $"{providerName} API key is not configured. Set {environmentVariable}.");
        }
    }
}
