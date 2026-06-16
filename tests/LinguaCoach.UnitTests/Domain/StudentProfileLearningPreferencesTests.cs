using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Unit tests for StudentProfile.UpdateLearningPreferences (Phase 10G).
/// Validates student-editable preferences. CEFR and admin fields must remain unchanged.
/// </summary>
public sealed class StudentProfileLearningPreferencesTests
{
    private static StudentProfile CreateProfile()
    {
        var profile = new StudentProfile(Guid.NewGuid());
        // Set initial admin-settable values to verify they are not touched
        profile.SetInitialProfile(
            firstName: "Jane",
            lastName: "Doe",
            displayName: "Jane Doe",
            careerContext: "Nurse",
            learningGoal: "Workplace English",
            preferredSessionDurationMinutes: 20,
            experienceLevel: null,
            roleFamiliarity: null);
        return profile;
    }

    [Fact]
    public void UpdateLearningPreferences_SetsAllFields()
    {
        var profile = CreateProfile();

        profile.UpdateLearningPreferences(
            preferredName: "Janie",
            supportLanguageCode: "fa",
            supportLanguageName: "Persian",
            translationHelpPreference: TranslationHelpPreference.WhenDifficult,
            learningGoals: new[] { "Day-to-day English", "Travel English" },
            customLearningGoal: "Aviation English",
            focusAreas: new[] { "Speaking", "Listening" },
            customFocusArea: "Interviews",
            difficultyPreference: DifficultyPreference.Balanced,
            preferredSessionDurationMinutes: 30);

        profile.PreferredName.Should().Be("Janie");
        profile.SupportLanguageCode.Should().Be("fa");
        profile.SupportLanguageName.Should().Be("Persian");
        profile.TranslationHelpPreference.Should().Be(TranslationHelpPreference.WhenDifficult);
        profile.LearningGoals.Should().BeEquivalentTo(new[] { "Day-to-day English", "Travel English" });
        profile.CustomLearningGoal.Should().Be("Aviation English");
        profile.FocusAreas.Should().BeEquivalentTo(new[] { "Speaking", "Listening" });
        profile.CustomFocusArea.Should().Be("Interviews");
        profile.DifficultyPreference.Should().Be(DifficultyPreference.Balanced);
        profile.PreferredSessionDurationMinutes.Should().Be(30);
        profile.LearningPreferencesUpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateLearningPreferences_PreferredName_Over100Chars_Throws()
    {
        var profile = CreateProfile();
        var longName = new string('A', 101);

        var act = () => profile.UpdateLearningPreferences(
            preferredName: longName,
            supportLanguageCode: null, supportLanguageName: null,
            translationHelpPreference: null, learningGoals: null,
            customLearningGoal: null, focusAreas: null,
            customFocusArea: null, difficultyPreference: null,
            preferredSessionDurationMinutes: null);

        act.Should().Throw<ArgumentException>().WithMessage("*PreferredName*");
    }

    [Fact]
    public void UpdateLearningPreferences_SupportLanguageCode_Over10Chars_Throws()
    {
        var profile = CreateProfile();

        var act = () => profile.UpdateLearningPreferences(
            preferredName: null,
            supportLanguageCode: "zh-hans-CN-X", // 12 chars
            supportLanguageName: null, translationHelpPreference: null,
            learningGoals: null, customLearningGoal: null,
            focusAreas: null, customFocusArea: null,
            difficultyPreference: null, preferredSessionDurationMinutes: null);

        act.Should().Throw<ArgumentException>().WithMessage("*SupportLanguageCode*");
    }

    [Fact]
    public void UpdateLearningPreferences_MoreThan10LearningGoals_Throws()
    {
        var profile = CreateProfile();
        var goals = Enumerable.Range(1, 11).Select(i => $"Goal {i}").ToList();

        var act = () => profile.UpdateLearningPreferences(
            preferredName: null, supportLanguageCode: null,
            supportLanguageName: null, translationHelpPreference: null,
            learningGoals: goals, customLearningGoal: null,
            focusAreas: null, customFocusArea: null,
            difficultyPreference: null, preferredSessionDurationMinutes: null);

        act.Should().Throw<ArgumentException>().WithMessage("*learning goal*");
    }

    [Fact]
    public void UpdateLearningPreferences_MoreThan10FocusAreas_Throws()
    {
        var profile = CreateProfile();
        var areas = Enumerable.Range(1, 11).Select(i => $"Area {i}").ToList();

        var act = () => profile.UpdateLearningPreferences(
            preferredName: null, supportLanguageCode: null,
            supportLanguageName: null, translationHelpPreference: null,
            learningGoals: null, customLearningGoal: null,
            focusAreas: areas, customFocusArea: null,
            difficultyPreference: null, preferredSessionDurationMinutes: null);

        act.Should().Throw<ArgumentException>().WithMessage("*focus area*");
    }

    [Fact]
    public void UpdateLearningPreferences_CustomLearningGoal_Over200Chars_Throws()
    {
        var profile = CreateProfile();
        var longGoal = new string('X', 201);

        var act = () => profile.UpdateLearningPreferences(
            preferredName: null, supportLanguageCode: null,
            supportLanguageName: null, translationHelpPreference: null,
            learningGoals: null, customLearningGoal: longGoal,
            focusAreas: null, customFocusArea: null,
            difficultyPreference: null, preferredSessionDurationMinutes: null);

        act.Should().Throw<ArgumentException>().WithMessage("*CustomLearningGoal*");
    }

    [Fact]
    public void UpdateLearningPreferences_CustomFocusArea_Over200Chars_Throws()
    {
        var profile = CreateProfile();
        var longArea = new string('X', 201);

        var act = () => profile.UpdateLearningPreferences(
            preferredName: null, supportLanguageCode: null,
            supportLanguageName: null, translationHelpPreference: null,
            learningGoals: null, customLearningGoal: null,
            focusAreas: null, customFocusArea: longArea,
            difficultyPreference: null, preferredSessionDurationMinutes: null);

        act.Should().Throw<ArgumentException>().WithMessage("*CustomFocusArea*");
    }

    [Fact]
    public void UpdateLearningPreferences_DoesNotChangeCefrLevel()
    {
        var profile = CreateProfile();
        profile.SetCefrLevel("B2");

        profile.UpdateLearningPreferences(
            preferredName: "Test",
            supportLanguageCode: null, supportLanguageName: null,
            translationHelpPreference: null, learningGoals: null,
            customLearningGoal: null, focusAreas: null,
            customFocusArea: null, difficultyPreference: null,
            preferredSessionDurationMinutes: null);

        profile.CefrLevel.Should().Be("B2");
    }

    [Fact]
    public void UpdateLearningPreferences_DoesNotChangeAdminFields()
    {
        var profile = CreateProfile();

        profile.UpdateLearningPreferences(
            preferredName: "Override",
            supportLanguageCode: null, supportLanguageName: null,
            translationHelpPreference: null, learningGoals: null,
            customLearningGoal: null, focusAreas: null,
            customFocusArea: null, difficultyPreference: null,
            preferredSessionDurationMinutes: null);

        // Admin-set fields must be unchanged
        profile.FirstName.Should().Be("Jane");
        profile.LastName.Should().Be("Doe");
        profile.DisplayName.Should().Be("Jane Doe");
        profile.CareerContext.Should().Be("Nurse");
        profile.LearningGoal.Should().Be("Workplace English");
    }

    [Fact]
    public void UpdateLearningPreferences_SetsTimestamp()
    {
        var profile = CreateProfile();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        profile.UpdateLearningPreferences(
            preferredName: null, supportLanguageCode: null,
            supportLanguageName: null, translationHelpPreference: null,
            learningGoals: null, customLearningGoal: null,
            focusAreas: null, customFocusArea: null,
            difficultyPreference: null, preferredSessionDurationMinutes: null);

        profile.LearningPreferencesUpdatedAt.Should().BeAfter(before);
    }

    [Fact]
    public void UpdateLearningPreferences_Exactly10LearningGoals_Succeeds()
    {
        var profile = CreateProfile();
        var goals = Enumerable.Range(1, 10).Select(i => $"Goal {i}").ToList();

        var act = () => profile.UpdateLearningPreferences(
            preferredName: null, supportLanguageCode: null,
            supportLanguageName: null, translationHelpPreference: null,
            learningGoals: goals, customLearningGoal: null,
            focusAreas: null, customFocusArea: null,
            difficultyPreference: null, preferredSessionDurationMinutes: null);

        act.Should().NotThrow();
        profile.LearningGoals.Should().HaveCount(10);
    }
}
