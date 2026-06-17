namespace LinguaCoach.Domain.Enums;

// Typed mapping: tells the step handler which StudentProfile field to update.
// Serialized as string in AnswerMappingJson so DB is human-readable.
public enum OnboardingAnswerMapping
{
    None = 0,
    PreferredName = 1,
    SupportLanguage = 2,         // sets SupportLanguageCode + SupportLanguageName + TranslationHelpPreference
    LearningGoals = 3,           // sets LearningGoals + CustomLearningGoal
    FocusAreas = 4,              // sets FocusAreas + CustomFocusArea
    DifficultyPreference = 5     // sets DifficultyPreference
}
