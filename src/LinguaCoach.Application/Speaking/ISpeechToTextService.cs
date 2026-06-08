namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Abstraction for server-side speech-to-text transcription.
/// MVP uses FakeSpeechToTextService (deterministic placeholder).
/// Real providers (OpenAI Whisper, Azure STT) wired in a later sprint.
/// </summary>
public interface ISpeechToTextService
{
    Task<SpeechToTextResult> TranscribeAsync(
        Stream audioStream,
        SpeechToTextOptions options,
        CancellationToken ct = default);
}

public sealed record SpeechToTextOptions(
    string AudioMimeType,
    string TargetLanguageCode,
    int? MaxDurationSeconds = null);

public sealed record SpeechToTextResult(
    bool Success,
    string? Transcript,
    string Provider,
    long DurationMs,
    string? FailureReason = null);
