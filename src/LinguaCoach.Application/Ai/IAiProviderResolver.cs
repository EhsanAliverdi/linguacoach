namespace LinguaCoach.Application.Ai;

public sealed record AiProviderSelection(
    IAiProvider Provider,
    string ProviderName,
    string ModelName);

public interface IAiProviderResolver
{
    AiProviderSelection ResolveWritingFeedbackProvider();
}
