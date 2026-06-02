namespace LinguaCoach.Application.Ai;

/// <summary>
/// Abstraction over an AI language model provider (e.g. OpenAI, Azure OpenAI).
/// Infrastructure implements this. Application and Domain never depend on a concrete provider.
/// Not implemented in MVP skeleton — defined here so DI registration and tests can wire stubs.
/// </summary>
public interface IAiProvider
{
    string ProviderName { get; }

    /// <summary>
    /// Sends a prompt to the AI provider and returns the raw JSON response string.
    /// The caller is responsible for deserialising the response to the appropriate type.
    /// </summary>
    Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default);
}

/// <summary>Input to an AI provider call.</summary>
public sealed record AiRequest(
    string PromptKey,
    string RenderedPrompt,
    int MaxOutputTokens,
    string ModelHint = "",
    string? ApiKeyOverride = null);

/// <summary>Output from an AI provider call.</summary>
public sealed record AiResponse(
    string ResponseJson,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    string ModelName = "",
    string ProviderName = "",
    string Status = "succeeded",
    string? Error = null);
