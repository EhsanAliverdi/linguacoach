using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Ai;

public sealed class GeminiProvider : IAiProvider
{
    private const string DefaultModel = "gemini-2.0-flash";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiProvider> _logger;

    public GeminiProvider(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public string ProviderName => "gemini";

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        // ApiKeyOverride (from DB) takes precedence over env/config.
        var apiKey = request.ApiKeyOverride
            ?? _configuration["Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiConfigurationUnavailableException(
                "Gemini API key is not configured.",
                new InvalidOperationException("Set Gemini:ApiKey, GEMINI_API_KEY, or store it via admin."));
        }

        var modelToUse = string.IsNullOrWhiteSpace(request.ModelHint) ? DefaultModel : request.ModelHint;
        // Gemini 2.5 models are served under v1beta; older models also work there.
        var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(modelToUse)}:generateContent";

        // Only request JSON output for real feature prompts; test pings use plain text.
        var isTestCall = request.PromptKey == "admin.test";
        var generationConfig = isTestCall
            ? new GeminiGenerationConfig(ResponseMimeType: null, MaxOutputTokens: request.MaxOutputTokens)
            : new GeminiGenerationConfig(ResponseMimeType: "application/json", MaxOutputTokens: request.MaxOutputTokens);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);
        httpRequest.Headers.Add("x-goog-api-key", apiKey);
        httpRequest.Content = JsonContent.Create(new GeminiGenerateContentRequest(
            Contents: [new GeminiContent(Parts: [new GeminiPart(request.RenderedPrompt)])],
            GenerationConfig: generationConfig),
            options: new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API call failed for prompt key {Key}", request.PromptKey);
            throw new AiProviderException($"Gemini call failed: {ex.Message}", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var snippet = responseBody.Length > 400 ? responseBody[..400] : responseBody;
            _logger.LogWarning(
                "Gemini API returned {StatusCode} for model {Model}. Body: {Body}",
                (int)response.StatusCode,
                modelToUse,
                snippet);
            throw new AiProviderException(
                $"Gemini {modelToUse} HTTP {(int)response.StatusCode}: {snippet}",
                new InvalidOperationException(responseBody));
        }

        GeminiGenerateContentResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new AiProviderException("Gemini response envelope was not valid JSON.", ex);
        }

        // Thinking models (2.5-pro, 2.5-flash) emit thought parts with Thought=true before the answer.
        // Skip those and find the first non-thought, non-empty text part.
        var responseText = parsed?.Candidates?
            .FirstOrDefault()?.Content?.Parts?
            .FirstOrDefault(part => !part.Thought && !string.IsNullOrWhiteSpace(part.Text))?.Text;

        if (string.IsNullOrWhiteSpace(responseText))
        {
            var snippet = responseBody.Length > 400 ? responseBody[..400] : responseBody;
            throw new AiProviderException(
                $"Gemini response did not include text content. Body: {snippet}",
                new InvalidOperationException(responseBody));
        }

        var inputTokens = parsed?.UsageMetadata?.PromptTokenCount ?? 0;
        var outputTokens = parsed?.UsageMetadata?.CandidatesTokenCount ?? 0;
        var pricing = AiPricingOptions.GetGeminiPricing(_configuration, modelToUse);
        var cost = pricing is null
            ? 0m
            : AiPricingOptions.EstimateCostUsd(inputTokens, outputTokens, pricing);

        if (pricing is null)
        {
            _logger.LogInformation(
                "Gemini call complete: key={Key} input={Input} output={Output}; pricing is not configured for model {Model}, so cost was not estimated.",
                request.PromptKey,
                inputTokens,
                outputTokens,
                modelToUse);
        }
        else
        {
            _logger.LogInformation(
                "Gemini call complete: key={Key} input={Input} output={Output} cost=${Cost:F6}",
                request.PromptKey,
                inputTokens,
                outputTokens,
                cost);
        }

        return new AiResponse(
            responseText,
            inputTokens,
            outputTokens,
            cost,
            modelToUse,
            ProviderName);
    }
}

internal sealed record GeminiGenerateContentRequest(
    [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
    [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

internal sealed record GeminiContent(
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

internal sealed record GeminiPart(
    [property: JsonPropertyName("text")] string Text);

internal sealed record GeminiGenerationConfig(
    [property: JsonPropertyName("responseMimeType")] string? ResponseMimeType,
    [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens);

internal sealed class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

internal sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContentResponse? Content { get; set; }
}

internal sealed class GeminiContentResponse
{
    [JsonPropertyName("parts")]
    public List<GeminiPartResponse>? Parts { get; set; }
}

internal sealed class GeminiPartResponse
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("thought")]
    public bool Thought { get; set; }
}

internal sealed class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }
}
