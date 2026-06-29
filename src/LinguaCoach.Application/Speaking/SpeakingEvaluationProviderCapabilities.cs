namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Describes what a speaking evaluation provider is capable of.
/// Used for admin status reporting and safe result parsing.
/// </summary>
public sealed record SpeakingEvaluationProviderCapabilities(
    bool SupportsAudioInput,
    bool SupportsTranscript,
    bool SupportsFluencyScore,
    bool SupportsPronunciationScore,
    bool SupportsStructuredOutput,
    bool SupportsRubricScoring)
{
    public static SpeakingEvaluationProviderCapabilities None => new(
        SupportsAudioInput: false,
        SupportsTranscript: false,
        SupportsFluencyScore: false,
        SupportsPronunciationScore: false,
        SupportsStructuredOutput: false,
        SupportsRubricScoring: false);

    public static SpeakingEvaluationProviderCapabilities OpenAiWhisperGpt => new(
        SupportsAudioInput: true,
        SupportsTranscript: true,
        SupportsFluencyScore: true,
        SupportsPronunciationScore: false,
        SupportsStructuredOutput: true,
        SupportsRubricScoring: true);
}
