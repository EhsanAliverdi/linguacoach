namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Abstraction for cloud-based text-to-speech synthesis.
/// MVP: not implemented. The browser handles TTS via window.speechSynthesis —
/// this interface is defined for future cloud TTS (Azure TTS, ElevenLabs) and for test injection.
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// Synthesises speech from text. Not called in MVP — browser TTS handles output.
    /// </summary>
    Task<TtsResult> SynthesizeAsync(
        string text,
        string targetLanguageCode,
        CancellationToken ct = default);
}

public sealed record TtsResult(byte[] AudioBytes, string MimeType);
