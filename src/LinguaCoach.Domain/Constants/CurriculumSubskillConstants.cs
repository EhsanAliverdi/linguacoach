namespace LinguaCoach.Domain.Constants;

/// <summary>
/// Canonical subskill identifiers — a finer-grained classification beneath
/// CurriculumSkillConstants. Optional metadata: every consumer treats a null/absent
/// subskill as "not yet classified", not as invalid.
/// Values are lowercase, dot-separated, and prefixed with their owning skill
/// (e.g. "reading.gist" belongs to CurriculumSkillConstants.Reading).
/// </summary>
public static class CurriculumSubskillConstants
{
    // Reading
    public const string ReadingGist = "reading.gist";
    public const string ReadingDetail = "reading.detail";
    public const string ReadingInference = "reading.inference";
    public const string ReadingVocabularyInContext = "reading.vocabulary_in_context";
    public const string ReadingScanning = "reading.scanning";

    // Listening
    public const string ListeningGist = "listening.gist";
    public const string ListeningDetail = "listening.detail";
    public const string ListeningInference = "listening.inference";
    public const string ListeningKeywordRecognition = "listening.keyword_recognition";
    public const string ListeningDictation = "listening.dictation";

    // Writing
    public const string WritingSentenceAccuracy = "writing.sentence_accuracy";
    public const string WritingParagraphCoherence = "writing.paragraph_coherence";
    public const string WritingEmailMessage = "writing.email_message";
    public const string WritingShortResponse = "writing.short_response";
    public const string WritingTaskAchievement = "writing.task_achievement";

    // Speaking
    public const string SpeakingPronunciation = "speaking.pronunciation";
    public const string SpeakingFluency = "speaking.fluency";
    public const string SpeakingCoherence = "speaking.coherence";
    public const string SpeakingRoleplay = "speaking.roleplay";
    public const string SpeakingTaskAchievement = "speaking.task_achievement";

    // Vocabulary
    public const string VocabularyReceptive = "vocabulary.receptive";
    public const string VocabularyProductive = "vocabulary.productive";
    public const string VocabularyCollocation = "vocabulary.collocation";
    public const string VocabularyWordForm = "vocabulary.word_form";
    public const string VocabularyPhrasalVerbs = "vocabulary.phrasal_verbs";

    // Grammar
    public const string GrammarTenseAspect = "grammar.tense_aspect";
    public const string GrammarWordOrder = "grammar.word_order";
    public const string GrammarQuestionForms = "grammar.question_forms";
    public const string GrammarArticlesDeterminers = "grammar.articles_determiners";
    public const string GrammarPrepositions = "grammar.prepositions";

    // Pronunciation
    public const string PronunciationSounds = "pronunciation.sounds";
    public const string PronunciationWordStress = "pronunciation.word_stress";
    public const string PronunciationSentenceStress = "pronunciation.sentence_stress";
    public const string PronunciationIntonation = "pronunciation.intonation";

    // Fluency
    public const string FluencyPacing = "fluency.pacing";
    public const string FluencyPauses = "fluency.pauses";
    public const string FluencySelfCorrection = "fluency.self_correction";

    // Confidence
    public const string ConfidenceSelfRating = "confidence.self_rating";
    public const string ConfidenceRiskTaking = "confidence.risk_taking";
    public const string ConfidenceCommunicationRepair = "confidence.communication_repair";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BySkill =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [CurriculumSkillConstants.Reading] =
            [
                ReadingGist, ReadingDetail, ReadingInference,
                ReadingVocabularyInContext, ReadingScanning
            ],
            [CurriculumSkillConstants.Listening] =
            [
                ListeningGist, ListeningDetail, ListeningInference,
                ListeningKeywordRecognition, ListeningDictation
            ],
            [CurriculumSkillConstants.Writing] =
            [
                WritingSentenceAccuracy, WritingParagraphCoherence, WritingEmailMessage,
                WritingShortResponse, WritingTaskAchievement
            ],
            [CurriculumSkillConstants.Speaking] =
            [
                SpeakingPronunciation, SpeakingFluency, SpeakingCoherence,
                SpeakingRoleplay, SpeakingTaskAchievement
            ],
            [CurriculumSkillConstants.Vocabulary] =
            [
                VocabularyReceptive, VocabularyProductive, VocabularyCollocation,
                VocabularyWordForm, VocabularyPhrasalVerbs
            ],
            [CurriculumSkillConstants.Grammar] =
            [
                GrammarTenseAspect, GrammarWordOrder, GrammarQuestionForms,
                GrammarArticlesDeterminers, GrammarPrepositions
            ],
            [CurriculumSkillConstants.Pronunciation] =
            [
                PronunciationSounds, PronunciationWordStress,
                PronunciationSentenceStress, PronunciationIntonation
            ],
            [CurriculumSkillConstants.Fluency] =
            [
                FluencyPacing, FluencyPauses, FluencySelfCorrection
            ],
            [CurriculumSkillConstants.Confidence] =
            [
                ConfidenceSelfRating, ConfidenceRiskTaking, ConfidenceCommunicationRepair
            ],
        };

    public static readonly IReadOnlyList<string> All = BySkill.Values.SelectMany(v => v).ToList();

    /// <summary>Subskill constants belonging to the given CurriculumSkillConstants value. Empty if the skill is unknown.</summary>
    public static IReadOnlyList<string> ForSkill(string skill) =>
        BySkill.TryGetValue(skill, out var subskills) ? subskills : [];

    public static bool IsValid(string? subskill) =>
        subskill is not null && All.Contains(subskill, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when subskill is null (unclassified — always allowed), or when subskill belongs
    /// to the given skill's subskill set. False if skill is unrecognized and subskill is non-null.
    /// </summary>
    public static bool IsValidForSkill(string skill, string? subskill) =>
        subskill is null || ForSkill(skill).Contains(subskill, StringComparer.OrdinalIgnoreCase);
}
