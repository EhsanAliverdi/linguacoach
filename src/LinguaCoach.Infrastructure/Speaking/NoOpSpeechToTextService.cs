using LinguaCoach.Application.Speaking;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Fallback stub used when STT provider is unknown or unconfigured.
/// </summary>
internal sealed class NoOpSpeechToTextService : ISpeechToTextService
{
    public Task<SpeechToTextResult> TranscribeAsync(
        Stream audioStream, SpeechToTextOptions options, CancellationToken ct = default)
        => Task.FromResult(new SpeechToTextResult(
            Success: false,
            Transcript: null,
            Provider: "NoOp",
            DurationMs: 0,
            FailureReason: "No STT provider is configured."));
}
