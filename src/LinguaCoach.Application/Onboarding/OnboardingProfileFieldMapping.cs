namespace LinguaCoach.Application.Onboarding;

/// <summary>
/// The single source of truth for how a submitted onboarding Form.io component's <c>key</c> maps
/// onto a <c>StudentProfile</c> field. <c>StudentOnboardingFlowService.ApplyToProfileAsync</c> is
/// the only code that actually reads these keys out of a submission — this list exists so that
/// (a) the admin onboarding editor can show the admin which keys the backend expects (via the
/// "Field mapping" slide-over) and (b) <see cref="AdminOnboardingTemplateService"/> can refuse to
/// publish a template that is missing a <see cref="Required"/> key, since a missing required key
/// means every future submission created for that template would silently omit that profile
/// field with no error surfaced anywhere. There is no dynamic registration mechanism — an admin
/// cannot introduce a new mapped key without a backend code change to
/// <c>StudentOnboardingFlowService</c>; this list must be kept in sync with that method by hand.
/// </summary>
public static class OnboardingProfileFieldMapping
{
    public sealed record FieldMapping(
        string Key,
        string ProfileField,
        string Description,
        bool Required,
        string ExpectedShape);

    public static readonly IReadOnlyList<FieldMapping> Fields = new[]
    {
        new FieldMapping(
            "preferred_name", "PreferredName",
            "What the student wants to be called.",
            Required: true,
            ExpectedShape: "Text value (textfield)."),
        new FieldMapping(
            "support_language", "SupportLanguageCode / SupportLanguageName",
            "Language code the student wants help in. Omit the component, or submit \"none\"/empty, to disable support-language help.",
            Required: false,
            ExpectedShape: "Select/radio value matching a Language.Code in the languages table (e.g. \"fa\")."),
        new FieldMapping(
            "learning_goals", "LearningGoals",
            "The student's learning goals.",
            Required: false,
            ExpectedShape: "selectboxes (submits {optionKey: true/false}) or any multi-value array."),
        new FieldMapping(
            "custom_learning_goal", "CustomLearningGoal",
            "Free-text goal shown when \"other\" is selected in learning_goals.",
            Required: false,
            ExpectedShape: "Text value (textfield)."),
        new FieldMapping(
            "focus_areas", "FocusAreas",
            "Skills the student wants to focus on.",
            Required: false,
            ExpectedShape: "selectboxes (submits {optionKey: true/false}) or any multi-value array."),
        new FieldMapping(
            "custom_focus_area", "CustomFocusArea",
            "Free-text focus area shown when \"other\" is selected in focus_areas.",
            Required: false,
            ExpectedShape: "Text value (textfield)."),
        new FieldMapping(
            "difficulty_preference", "DifficultyPreference",
            "How challenging the student wants practice to be.",
            Required: false,
            ExpectedShape: "Radio/select value matching a DifficultyPreference enum name exactly: Gentle, Balanced, or Challenging."),
        new FieldMapping(
            "session_duration", "PreferredSessionDurationMinutes",
            "Preferred practice session length.",
            Required: false,
            ExpectedShape: "Select value parseable as a positive integer (minutes), e.g. \"15\"."),
        new FieldMapping(
            "career_context", "CareerContext",
            "Free-text note about the student's career/professional context.",
            Required: false,
            ExpectedShape: "Text value (textfield/textarea)."),
        new FieldMapping(
            "professional_experience_level", "ProfessionalExperienceLevel",
            "The student's professional experience level.",
            Required: false,
            ExpectedShape: "Radio/select value matching a ProfessionalExperienceLevel enum name exactly: NoProfessionalExperience, EntryLevelOrGraduate, Junior_0_2Years, MidLevel_2_5Years, Senior_5_10Years, or LeadOrManager_10PlusYears."),
        new FieldMapping(
            "role_familiarity", "RoleFamiliarity",
            "How familiar the student is with their current/target role.",
            Required: false,
            ExpectedShape: "Radio/select value matching a RoleFamiliarity enum name exactly: NewToRole, UnderstandsBasics, CurrentlyWorkingInRole, ExperiencedInRole, or ManagesOrTrainsOthers."),
    };

    public static readonly IReadOnlyCollection<string> RequiredKeys =
        Fields.Where(f => f.Required).Select(f => f.Key).ToArray();
}
