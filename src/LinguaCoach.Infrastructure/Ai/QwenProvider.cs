using System.ClientModel;
using LinguaCoach.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace LinguaCoach.Infrastructure.Ai;

/// <summary>
/// Qwen (Alibaba Cloud Model Studio / DashScope) provider.
/// Uses the OpenAI-compatible chat completion endpoint.
/// Base URL defaults to DashScope global; override with Qwen:OpenAiCompatible config
/// (e.g. https://&lt;workspace&gt;.ap-southeast-1.maas.aliyuncs.com/compatible-mode/v1).
/// API key: QWEN_API_KEY environment variable or Qwen:ApiKey config.
/// </summary>
public sealed class QwenProvider : IAiProvider
{
    public string ProviderName => "qwen";

    private const string DefaultBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
    private const string DefaultModel = "qwen-plus";

    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly string? _defaultApiKey;
    private readonly ILogger<QwenProvider> _logger;
    private readonly IConfiguration _configuration;

    public QwenProvider(IConfiguration configuration, ILogger<QwenProvider> logger)
    {
        _logger = logger;
        _configuration = configuration;
        _baseUrl = configuration["Qwen:OpenAiCompatible"] ?? DefaultBaseUrl;
        _defaultModel = configuration["Qwen:Model"] ?? DefaultModel;
        _defaultApiKey = configuration["Qwen:ApiKey"] ?? Environment.GetEnvironmentVariable("QWEN_API_KEY");

        if (string.IsNullOrWhiteSpace(_defaultApiKey))
        {
            _logger.LogWarning(
                "Qwen API key is not configured. Set QWEN_API_KEY or Qwen:ApiKey for Qwen-backed features.");
        }
    }

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        var apiKey = !string.IsNullOrWhiteSpace(request.ApiKeyOverride)
            ? request.ApiKeyOverride
            : _defaultApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiConfigurationUnavailableException(
                "Qwen API key is not configured.",
                new InvalidOperationException("Set QWEN_API_KEY, Qwen:ApiKey, or store it via admin."));
        }

        var modelToUse = string.IsNullOrEmpty(request.ModelHint) ? _defaultModel : request.ModelHint;
        _logger.LogDebug("Calling Qwen model {Model} for prompt key {Key}", modelToUse, request.PromptKey);

        var credential = new ApiKeyCredential(apiKey);
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(_baseUrl) };
        var chatClient = new ChatClient(modelToUse, credential, clientOptions);

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxOutputTokens,
        };

        ClientResult<ChatCompletion> result;
        try
        {
            result = await chatClient.CompleteChatAsync(
                new[] { new UserChatMessage(request.RenderedPrompt) }, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qwen API call failed for prompt key {Key}", request.PromptKey);
            throw new AiProviderException($"Qwen call failed: {ex.Message}", ex);
        }

        var completion = result.Value;
        var responseText = completion.Content[0].Text;

        var inputTokens = completion.Usage.InputTokenCount;
        var outputTokens = completion.Usage.OutputTokenCount;

        _logger.LogInformation(
            "Qwen call complete: key={Key} model={Model} input={Input} output={Output}",
            request.PromptKey, modelToUse, inputTokens, outputTokens);

        return new AiResponse(responseText, inputTokens, outputTokens, 0m, modelToUse, ProviderName);
    }
}
