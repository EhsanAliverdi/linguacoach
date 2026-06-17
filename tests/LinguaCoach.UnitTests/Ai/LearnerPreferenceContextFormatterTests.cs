using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;

namespace LinguaCoach.UnitTests.Ai;

public sealed class LearnerPreferenceContextFormatterTests
{
    [Fact]
    public void Build_IncludesPresentLearningPreferencesAndSystemEstimatedCefr()
    {
        var profile = NewProfile();
        profile.SetCefrLevel("b1");
        profile.UpdateLearningPreferences(
            preferredName: "Ehsan",
            supportLanguageCode: "fa",
            supportLanguageName: "Persian",
            translationHelpPreference: TranslationHelpPreference.WhenDifficult,
            learningGoals: ["Travel English", "Day-to-day English"],
            customLearningGoal: "Feel confident speaking with neighbours.",
            focusAreas: ["Speaking", "Listening", "Pronunciation"],
            customFocusArea: "Airport conversations",
            difficultyPreference: DifficultyPreference.Balanced,
            preferredSessionDurationMinutes: null);

        var context = LearnerPreferenceContextFormatter.Build(profile, "English");

        context.Should().Contain("Preferred name: Ehsan");
        context.Should().Contain("Learning language: English");
        context.Should().Contain("Support language: Persian");
        context.Should().Contain("Translation help: when difficult");
        context.Should().Contain("Goals: Travel English, Day-to-day English");
        context.Should().Contain("Custom goal: Feel confident speaking with neighbours.");
        context.Should().Contain("Focus areas: Speaking, Listening, Pronunciation");
        context.Should().Contain("Custom focus: Airport conversations");
        context.Should().Contain("Difficulty preference: balanced");
        context.Should().Contain("Current level: B1 (system-estimated)");
    }

    [Fact]
    public void Build_WithMissingPreferences_DoesNotCreateFakeDefaults()
    {
        var profile = NewProfile();

        var context = LearnerPreferenceContextFormatter.Build(profile, null);

        context.Should().BeEmpty();
        context.ToLowerInvariant().Should().NotContain("workplace");
    }

    [Fact]
    public void Build_ExcludesAdminAndPromptFields()
    {
        var profile = NewProfile();
        profile.SetInitialProfile(
            firstName: "AdminFirst",
            lastName: "AdminLast",
            displayName: "AdminDisplay",
            careerContext: "Secret career context",
            learningGoal: "Legacy goal",
            preferredSessionDurationMinutes: null,
            experienceLevel: null,
            roleFamiliarity: null);
        profile.UpdateLearningPreferences(
            preferredName: "Learner",
            supportLanguageCode: null,
            supportLanguageName: null,
            translationHelpPreference: null,
            learningGoals: null,
            customLearningGoal: null,
            focusAreas: null,
            customFocusArea: null,
            difficultyPreference: null,
            preferredSessionDurationMinutes: null);

        var context = LearnerPreferenceContextFormatter.Build(profile, "English");

        context.Should().Contain("Preferred name: Learner");
        context.Should().NotContain("AdminFirst");
        context.Should().NotContain("AdminLast");
        context.Should().NotContain("AdminDisplay");
        context.Should().NotContain("Secret career context");
        context.Should().NotContain("Legacy goal");
    }

    [Fact]
    public void BuildLearningGoalContext_UsesPreferencePriorityBeforeLegacyFallbacks()
    {
        var profile = NewProfile();
        profile.SetInitialProfile(
            firstName: null,
            lastName: null,
            displayName: null,
            careerContext: "Civil engineering",
            learningGoal: "Workplace English",
            preferredSessionDurationMinutes: null,
            experienceLevel: null,
            roleFamiliarity: null);
        profile.UpdateLearningPreferences(
            preferredName: null,
            supportLanguageCode: null,
            supportLanguageName: null,
            translationHelpPreference: null,
            learningGoals: ["Travel English", "Study English"],
            customLearningGoal: "Talk confidently with neighbours.",
            focusAreas: null,
            customFocusArea: null,
            difficultyPreference: null,
            preferredSessionDurationMinutes: null);

        var context = LearnerPreferenceContextFormatter.BuildLearningGoalContext(profile);

        context.Should().Be("Talk confidently with neighbours.");
    }

    [Fact]
    public void BuildLearningGoalContext_FallsBackWithoutDefaultingToWorkplace()
    {
        var profile = NewProfile();

        var context = LearnerPreferenceContextFormatter.BuildLearningGoalContext(profile);

        context.Should().BeNull();
    }

    private static StudentProfile NewProfile() => new(Guid.NewGuid());
}
