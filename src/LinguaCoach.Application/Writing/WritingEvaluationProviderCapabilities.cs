namespace LinguaCoach.Application.Writing;

/// <summary>
/// Describes what a writing evaluation provider is capable of.
/// Used for admin status reporting and safe result parsing.
/// </summary>
public sealed record WritingEvaluationProviderCapabilities(
    bool SupportsTextInput,
    bool SupportsGrammarScore,
    bool SupportsVocabularyScore,
    bool SupportsCoherenceScore,
    bool SupportsCorrectedText,
    bool SupportsStructuredOutput,
    bool SupportsRubricScoring)
{
    public static WritingEvaluationProviderCapabilities None => new(
        SupportsTextInput: false,
        SupportsGrammarScore: false,
        SupportsVocabularyScore: false,
        SupportsCoherenceScore: false,
        SupportsCorrectedText: false,
        SupportsStructuredOutput: false,
        SupportsRubricScoring: false);

    public static WritingEvaluationProviderCapabilities OpenAiGpt => new(
        SupportsTextInput: true,
        SupportsGrammarScore: true,
        SupportsVocabularyScore: true,
        SupportsCoherenceScore: true,
        SupportsCorrectedText: true,
        SupportsStructuredOutput: true,
        SupportsRubricScoring: true);
}
