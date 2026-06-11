namespace LinguaCoach.Application.Speaking;

public interface ITextToSpeechService
{
    Task<TtsResult> GenerateSpeechAsync(
        string text,
        TextToSpeechOptions options,
        CancellationToken ct = default);
}

public sealed record TextToSpeechOptions(
    string TargetLanguageCode,
    string? Voice = null,
    string? Model = null,
    string? ApiKeyOverride = null,
    string? EndpointOverride = null);

public sealed record TtsResult(
    bool Success,
    byte[]? AudioBytes,
    string AudioContentType,
    string Provider,
    string Voice,
    long DurationMs,
    string? FailureReason = null);
