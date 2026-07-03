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
    DifficultyPreference = 5,    // sets DifficultyPreference
    CareerContext = 6,           // sets CareerContext (free text)
    SessionDuration = 7,         // sets PreferredSessionDurationMinutes
    WorkExperience = 8,          // sets ProfessionalExperienceLevel + RoleFamiliarity (legacy composite step)
    LearningGoalDescription = 9, // sets LearningGoalDescription (free text)
    ProfessionalExperienceLevel = 10, // sets ProfessionalExperienceLevel only (Phase 6b: WorkExperience split into two steps)
    RoleFamiliarity = 11,             // sets RoleFamiliarity only (Phase 6b)
    CustomLearningGoal = 12,          // sets CustomLearningGoal only (Phase 6b: split from LearningGoals into its own follow-up step)
    CustomFocusArea = 13              // sets CustomFocusArea only (Phase 6b: split from FocusAreas into its own follow-up step)
}
