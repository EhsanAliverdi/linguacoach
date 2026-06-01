using LinguaCoach.Application.Speaking;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// MVP stub. Browser uses window.speechSynthesis; this service is never called.
/// Replaced by a cloud TTS provider (Azure TTS, ElevenLabs) in a later task.
/// </summary>
internal sealed class NoOpTextToSpeechService : ITextToSpeechService
{
    public Task<TtsResult> SynthesizeAsync(string text, string targetLanguageCode, CancellationToken ct = default)
        => throw new NotSupportedException("Cloud TTS is not implemented in MVP. Browser TTS handles output.");
}
