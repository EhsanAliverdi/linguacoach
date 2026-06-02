using System.ClientModel;
using LinguaCoach.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace LinguaCoach.Infrastructure.Ai;

/// <summary>
/// OpenAI implementation of IAiProvider.
/// Reads the API key from configuration key "OpenAI:ApiKey" or environment variable OPENAI_API_KEY.
/// The model defaults to "gpt-4o" but can be overridden via "OpenAI:Model".
/// </summary>
public sealed class OpenAiProvider : IAiProvider
{
    public string ProviderName => "openai";

    private readonly string _model;
    private readonly ChatClient? _client;
    private readonly ApiKeyCredential? _credential;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiProvider> _logger;

    public OpenAiProvider(IConfiguration configuration, ILogger<OpenAiProvider> logger)
    {
        _logger = logger;
        _configuration = configuration;

        _model = configuration["OpenAI:Model"] ?? "gpt-4o";
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "OpenAI API key is not configured. AI-backed features will return a controlled unavailable response.");
            return;
        }

        _credential = new ApiKeyCredential(apiKey);
        _client = new ChatClient(_model, _credential);
    }

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        if (_client is null || _credential is null)
        {
            throw new AiProviderException(
                "OpenAI API key is not configured.",
                new InvalidOperationException("Set OpenAI:ApiKey or the OPENAI_API_KEY environment variable."));
        }

        var modelToUse = string.IsNullOrEmpty(request.ModelHint) ? _model : request.ModelHint;
        _logger.LogDebug("Calling OpenAI model {Model} for prompt key {Key}", modelToUse, request.PromptKey);

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxOutputTokens,
        };

        var messages = new[]
        {
            new UserChatMessage(request.RenderedPrompt)
        };

        ClientResult<ChatCompletion> result;
        try
        {
            var chatClient = modelToUse == _model
                ? _client
                : new ChatClient(modelToUse, _credential);
            result = await chatClient.CompleteChatAsync(messages, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API call failed for prompt key {Key}", request.PromptKey);
            throw new AiProviderException($"OpenAI call failed: {ex.Message}", ex);
        }

        var completion = result.Value;
        var responseText = completion.Content[0].Text;

        var inputTokens = completion.Usage.InputTokenCount;
        var outputTokens = completion.Usage.OutputTokenCount;
        var pricing = AiPricingOptions.GetOpenAiPricing(_configuration, modelToUse);
        var cost = pricing is null
            ? 0m
            : AiPricingOptions.EstimateCostUsd(inputTokens, outputTokens, pricing);

        if (pricing is null)
        {
            _logger.LogInformation(
                "OpenAI call complete: key={Key} input={Input} output={Output}; pricing is not configured for model {Model}, so cost was not estimated.",
                request.PromptKey, inputTokens, outputTokens, modelToUse);
        }
        else
        {
            _logger.LogInformation(
                "OpenAI call complete: key={Key} input={Input} output={Output} cost=${Cost:F6}",
                request.PromptKey, inputTokens, outputTokens, cost);
        }

        return new AiResponse(responseText, inputTokens, outputTokens, cost, modelToUse);
    }
}

/// <summary>Wraps provider-specific errors into a stable application exception.</summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException(string message, Exception inner) : base(message, inner) { }
}
