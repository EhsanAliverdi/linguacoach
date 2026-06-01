using LinguaCoach.Application.Speaking;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// MVP stub. Browser sends the transcript directly; this service is never called.
/// Replaced by a cloud STT provider (Whisper, Azure) in a later task.
/// </summary>
internal sealed class NoOpSpeechToTextService : ISpeechToTextService
{
    public Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream, string audioMimeType, string targetLanguageCode, CancellationToken ct = default)
        => throw new NotSupportedException("Cloud STT is not implemented in MVP. Browser sends transcript directly.");
}
