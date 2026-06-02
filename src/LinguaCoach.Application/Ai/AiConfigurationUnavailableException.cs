namespace LinguaCoach.Application.Ai;

/// <summary>
/// Thrown when an AI-backed feature cannot run because required configuration is missing.
/// </summary>
public sealed class AiConfigurationUnavailableException : Exception
{
    public AiConfigurationUnavailableException(string message) : base(message) { }

    public AiConfigurationUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
