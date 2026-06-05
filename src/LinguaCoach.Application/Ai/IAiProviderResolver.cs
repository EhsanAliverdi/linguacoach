namespace LinguaCoach.Application.Ai;

public sealed record AiProviderSelection(
    IAiProvider Provider,
    string ProviderName,
    string ModelName,
    string? ApiKeyOverride = null);

public sealed record AiProviderPair(
    AiProviderSelection Primary,
    AiProviderSelection? Fallback);

public interface IAiProviderResolver
{
    /// <summary>Resolves primary (and optional fallback) providers for a feature key.</summary>
    AiProviderPair ResolveWithFallback(string featureKey);

    /// <summary>Backward-compatible single-provider resolution.</summary>
    AiProviderSelection ResolveWritingFeedbackProvider();
}
