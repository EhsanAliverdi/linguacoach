using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using LinguaCoach.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Ai;

public sealed class AnthropicProvider : IAiProvider
{
    private const string DefaultModel = "claude-sonnet-4-6";
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnthropicProvider> _logger;

    public AnthropicProvider(IConfiguration configuration, ILogger<AnthropicProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string ProviderName => "anthropic";

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        // ApiKeyOverride (from DB) takes precedence over env/config.
        var apiKey = request.ApiKeyOverride
            ?? _configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiConfigurationUnavailableException(
                "Anthropic API key is not configured.",
                new InvalidOperationException("Set Anthropic:ApiKey, ANTHROPIC_API_KEY, or store it via admin."));
        }

        var modelToUse = string.IsNullOrWhiteSpace(request.ModelHint) ? DefaultModel : request.ModelHint;

        var client = new AnthropicClient(apiKey);
        var parameters = new MessageParameters
        {
            Messages = [new Message(RoleType.User, request.RenderedPrompt)],
            Model = modelToUse,
            MaxTokens = request.MaxOutputTokens,
            Stream = false,
        };

        MessageResponse result;
        try
        {
            result = await client.Messages.GetClaudeMessageAsync(parameters, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic API call failed for prompt key {Key}", request.PromptKey);
            throw new AiProviderException($"Anthropic call failed: {ex.Message}", ex);
        }

        var responseText = result.Message.ToString();
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new AiProviderException(
                "Anthropic response did not include text content.",
                new InvalidOperationException("Empty response from Anthropic API."));
        }

        var inputTokens = result.Usage?.InputTokens ?? 0;
        var outputTokens = result.Usage?.OutputTokens ?? 0;
        var pricing = AiPricingOptions.GetProviderPricing(_configuration, "Anthropic", modelToUse);
        var cost = pricing is null
            ? 0m
            : AiPricingOptions.EstimateCostUsd(inputTokens, outputTokens, pricing);

        if (pricing is null)
        {
            _logger.LogInformation(
                "Anthropic call complete: key={Key} input={Input} output={Output}; pricing not configured for model {Model}.",
                request.PromptKey, inputTokens, outputTokens, modelToUse);
        }
        else
        {
            _logger.LogInformation(
                "Anthropic call complete: key={Key} input={Input} output={Output} cost=${Cost:F6}",
                request.PromptKey, inputTokens, outputTokens, cost);
        }

        return new AiResponse(responseText, inputTokens, outputTokens, cost, modelToUse, ProviderName);
    }
}
