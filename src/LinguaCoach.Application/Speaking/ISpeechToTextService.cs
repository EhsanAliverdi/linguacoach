namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Abstraction for cloud-based speech-to-text transcription.
/// MVP: not implemented. The browser handles STT via Web Speech API and sends
/// the transcript directly — this interface is defined for future cloud STT
/// (OpenAI Whisper, Azure STT) and for test injection.
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// Transcribes audio to text. Not called in MVP — browser sends transcript directly.
    /// </summary>
    Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string audioMimeType,
        string targetLanguageCode,
        CancellationToken ct = default);
}

public sealed record TranscriptionResult(string Transcript, double? ConfidenceScore);
