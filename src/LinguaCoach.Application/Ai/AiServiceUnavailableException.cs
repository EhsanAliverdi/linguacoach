namespace LinguaCoach.Application.Ai;

/// <summary>
/// Thrown when an AI-backed feature cannot run because the provider is not configured,
/// is set to 'fake', or the upstream AI call failed at runtime.
/// Controllers should map this to HTTP 503.
/// </summary>
public sealed class AiServiceUnavailableException : Exception
{
    public string FeatureKey { get; }

    public AiServiceUnavailableException(string featureKey)
        : base($"AI service is not available for feature '{featureKey}'.")
    {
        FeatureKey = featureKey;
    }

    public AiServiceUnavailableException(string featureKey, Exception inner)
        : base($"AI service is not available for feature '{featureKey}'.", inner)
    {
        FeatureKey = featureKey;
    }
}
