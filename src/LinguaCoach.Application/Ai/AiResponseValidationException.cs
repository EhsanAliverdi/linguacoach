namespace LinguaCoach.Application.Ai;

/// <summary>
/// Thrown when a provider response cannot be parsed into the required structured payload.
/// </summary>
public sealed class AiResponseValidationException : Exception
{
    public AiResponseValidationException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
