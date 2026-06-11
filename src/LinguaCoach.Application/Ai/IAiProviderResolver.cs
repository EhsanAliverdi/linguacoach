namespace LinguaCoach.Application.Ai;

public sealed record AiProviderSelection(
    IAiProvider Provider,
    string ProviderName,
    string ModelName,
    string? ApiKeyOverride = null,
    string? EndpointOverride = null);

public sealed record AiProviderPair(
    AiProviderSelection Primary,
    AiProviderSelection? Fallback);

public sealed record AiTtsProviderSelection(
    string ProviderName,
    string? ModelName,
    string? VoiceName,
    string? ApiKeyOverride = null,
    string? EndpointOverride = null);

public interface IAiProviderResolver
{
    /// <summary>Resolves the configured LLM provider for a feature key and category.</summary>
    AiProviderPair ResolveLlm(string featureKey, string categoryKey);

    /// <summary>Resolves the configured TTS provider for a feature key and category.</summary>
    AiTtsProviderSelection ResolveTts(string featureKey, string categoryKey);
}
