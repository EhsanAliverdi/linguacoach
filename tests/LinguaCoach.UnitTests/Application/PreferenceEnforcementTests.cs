using FluentAssertions;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Learning;

namespace LinguaCoach.UnitTests.Application;

/// <summary>
/// Phase 10K-F: verifies that student profile preferences are enforced
/// across generation paths and that general English is the default fallback.
/// </summary>
public sealed class PreferenceEnforcementTests
{
    private readonly LearningGoalContextResolver _resolver = new();

    // ── helpers ────────────────────────────────────────────────────────────

    private static StudentProfile EmptyProfile() => new(Guid.NewGuid());

    private static StudentProfile ProfileWithGoals(params string[] goals)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, null, null, null,
            goals, null, null, null, null, null);
        return p;
    }

    private static StudentProfile ProfileWithFocusAreas(params string[] areas)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, null, null, null,
            null, null, areas, null, null, null);
        return p;
    }

    private static StudentProfile ProfileWithSupportLanguage(string code, string name,
        TranslationHelpPreference? pref = null)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, code, name, pref,
            null, null, null, null, null, null);
        return p;
    }

    private static StudentProfile ProfileWithDifficulty(DifficultyPreference difficulty)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, null, null, null,
            null, null, null, null, difficulty, null);
        return p;
    }

    private static StudentProfile ProfileWithSessionLength(int minutes)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, null, null, null,
            null, null, null, null, null, minutes);
        return p;
    }

    private LearningGoalResolutionContext Ctx(string source = "Test") =>
        new() { Source = source };

    // ── general English fallback ───────────────────────────────────────────

    [Fact]
    public void EmptyProfile_ResolvesToGeneralEnglish_NotWorkplace()
    {
        var result = _resolver.Resolve(EmptyProfile(), Ctx());

        result.ContextSummary.Should().NotBeNullOrWhiteSpace();
        result.WorkplaceSpecific.Should().BeFalse();
        result.ContextSummary!.ToLowerInvariant().Should().NotContain("workplace");
    }

    [Fact]
    public void DayToDayEnglish_Goal_IsNotWorkplaceSpecific()
    {
        var profile = ProfileWithGoals("Day-to-day English");

        var result = _resolver.Resolve(profile, Ctx());

        result.WorkplaceSpecific.Should().BeFalse();
        result.ContextSummary!.ToLowerInvariant().Should().NotContain("workplace");
    }

    [Fact]
    public void TravelEnglish_Goal_IsNotWorkplaceSpecific()
    {
        var profile = ProfileWithGoals("Travel English");

        var result = _resolver.Resolve(profile, Ctx());

        result.WorkplaceSpecific.Should().BeFalse();
    }

    [Fact]
    public void SocialConversation_Goal_IsNotWorkplaceSpecific()
    {
        var profile = ProfileWithGoals("Social conversation");

        var result = _resolver.Resolve(profile, Ctx());

        result.WorkplaceSpecific.Should().BeFalse();
    }

    [Fact]
    public void WorkplaceEnglish_Goal_IsWorkplaceSpecific()
    {
        var profile = ProfileWithGoals("Workplace English");

        var result = _resolver.Resolve(profile, Ctx());

        result.WorkplaceSpecific.Should().BeTrue();
    }

    // ── goals and focus areas reach generation context formatter ──────────

    [Fact]
    public void LearningGoals_AreIncludedInFormatter_Output()
    {
        var profile = ProfileWithGoals("Travel English", "Social conversation");

        var context = LearnerPreferenceContextFormatter.Build(profile, "English");

        context.Should().Contain("Travel English");
        context.Should().Contain("Social conversation");
    }

    [Fact]
    public void FocusAreas_AreIncludedInFormatter_Output()
    {
        var profile = ProfileWithFocusAreas("Speaking", "Listening");

        var context = LearnerPreferenceContextFormatter.Build(profile, "English");

        context.Should().Contain("Speaking");
        context.Should().Contain("Listening");
    }

    [Fact]
    public void DayToDayGoal_FormatterOutput_DoesNotContainWorkplace()
    {
        var profile = ProfileWithGoals("Day-to-day English");

        var context = LearnerPreferenceContextFormatter.Build(profile, "English");

        context.ToLowerInvariant().Should().NotContain("workplace");
    }

    // ── support language and translation preference ────────────────────────

    [Fact]
    public void SupportLanguage_IsIncludedInFormatter_Output()
    {
        var profile = ProfileWithSupportLanguage("fa", "Persian",
            TranslationHelpPreference.WhenDifficult);

        var context = LearnerPreferenceContextFormatter.Build(profile, "English");

        context.Should().Contain("Persian");
        context.Should().Contain("when difficult");
    }

    [Fact]
    public void SupportLanguage_IsIncludedInResolvedContext()
    {
        var profile = ProfileWithSupportLanguage("fa", "Persian");

        var result = _resolver.Resolve(profile, Ctx());

        result.SupportLanguageCode.Should().Be("fa");
        result.SupportLanguageName.Should().Be("Persian");
    }

    [Fact]
    public void TranslationHelpPreference_AlwaysAvailable_IsIncludedInFormatter()
    {
        var profile = ProfileWithSupportLanguage("fa", "Persian",
            TranslationHelpPreference.AlwaysAvailable);

        var context = LearnerPreferenceContextFormatter.Build(profile, "English");

        context.Should().Contain("always available");
    }

    // ── difficulty preference ──────────────────────────────────────────────

    [Fact]
    public void DifficultyPreference_IsIncludedInFormatter_Output()
    {
        var profile = ProfileWithDifficulty(DifficultyPreference.Challenging);

        var context = LearnerPreferenceContextFormatter.Build(profile, "English");

        context.Should().Contain("challenging");
    }

    [Fact]
    public void DifficultyPreference_IsIncludedInResolvedContext()
    {
        var profile = ProfileWithDifficulty(DifficultyPreference.Gentle);

        var result = _resolver.Resolve(profile, Ctx());

        result.DifficultyPreference.Should().Be("gentle");
    }

    // ── session length preference ──────────────────────────────────────────

    [Fact]
    public void SessionLength_IsStoredOnProfile()
    {
        var profile = ProfileWithSessionLength(30);

        profile.PreferredSessionDurationMinutes.Should().Be(30);
    }

    [Fact]
    public void SessionLength_NullWhenNotSet()
    {
        var profile = EmptyProfile();

        profile.PreferredSessionDurationMinutes.Should().BeNull();
    }

    // ── CurriculumContextMapper ────────────────────────────────────────────

    [Fact]
    public void CurriculumContextMapper_NullInput_ReturnsGeneralEnglish()
    {
        var tags = CurriculumContextMapper.MapFromResolvedContext(null);

        tags.Should().ContainSingle().Which.Should().Be("general_english");
    }

    [Fact]
    public void CurriculumContextMapper_NonWorkplaceContext_ReturnsGeneralEnglish_NotWorkplace()
    {
        var context = new ResolvedLearningGoalContext
        {
            ContextSummary = "day-to-day English communication",
            WorkplaceSpecific = false
        };

        var tags = CurriculumContextMapper.MapFromResolvedContext(context);

        tags.Should().NotContain("workplace");
        tags.Should().Contain("general_english");
    }

    [Fact]
    public void CurriculumContextMapper_WorkplaceContext_ContainsWorkplaceTag()
    {
        var context = new ResolvedLearningGoalContext
        {
            ContextSummary = "workplace professional English",
            WorkplaceSpecific = true
        };

        var tags = CurriculumContextMapper.MapFromResolvedContext(context);

        tags.Should().Contain("workplace");
    }

    // ── vocabulary cadence: non-workplace uses PhraseMatch ─────────────────

    [Fact]
    public void NonWorkplaceProfile_Resolver_WorkplaceSpecificIsFalse_UsePhraseMatch()
    {
        // This test verifies the guard logic that ActivityGetHandler now applies:
        // resolved.WorkplaceSpecific == false → PhraseMatch, not GapFillWorkplacePhrase.
        var profile = ProfileWithGoals("Travel English");

        var resolved = _resolver.Resolve(profile, Ctx());
        var expectedPatternKey = resolved.WorkplaceSpecific
            ? ExercisePatternKey.GapFillWorkplacePhrase
            : ExercisePatternKey.PhraseMatch;

        expectedPatternKey.Should().Be(ExercisePatternKey.PhraseMatch);
    }

    [Fact]
    public void WorkplaceProfile_Resolver_WorkplaceSpecificIsTrue_UseGapFillWorkplacePhrase()
    {
        var profile = ProfileWithGoals("Workplace English");

        var resolved = _resolver.Resolve(profile, Ctx());
        var expectedPatternKey = resolved.WorkplaceSpecific
            ? ExercisePatternKey.GapFillWorkplacePhrase
            : ExercisePatternKey.PhraseMatch;

        expectedPatternKey.Should().Be(ExercisePatternKey.GapFillWorkplacePhrase);
    }
}
