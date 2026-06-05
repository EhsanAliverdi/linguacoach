namespace LinguaCoach.Application.Ai;

/// <summary>
/// Thrown when all configured AI providers (primary + fallback) have failed.
/// Caught by GlobalExceptionMiddleware to return a user-friendly "AI unavailable" response.
/// </summary>
public sealed class AiUnavailableException : Exception
{
    public AiUnavailableException(string message) : base(message) { }
    public AiUnavailableException(string message, Exception inner) : base(message, inner) { }
}
