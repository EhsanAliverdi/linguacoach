namespace LinguaCoach.Application.Ai;

public sealed record AiProviderSelection(
    IAiProvider Provider,
    string ProviderName,
    string ModelName,
    string? ApiKeyOverride = null);

public interface IAiProviderResolver
{
    AiProviderSelection ResolveWritingFeedbackProvider();
}
