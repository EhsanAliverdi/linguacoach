using FluentAssertions;
using LinguaCoach.Application.Learning;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Learning;
using Xunit;

namespace LinguaCoach.UnitTests.Application;

public sealed class LearningGoalContextResolverTests
{
    private readonly LearningGoalContextResolver _sut = new();

    // ── helpers ────────────────────────────────────────────────────────────

    private static StudentProfile EmptyProfile() => new(Guid.NewGuid());

    private static StudentProfile ProfileWithGoals(params string[] goals)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, null, null, null,
            goals, null, null, null, null, null);
        return p;
    }

    private static StudentProfile ProfileWithCustomGoal(string custom)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, null, null, null,
            null, custom, null, null, null, null);
        return p;
    }

    private static StudentProfile ProfileWithFocusAreas(params string[] areas)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, null, null, null,
            null, null, areas, null, null, null);
        return p;
    }

    private static StudentProfile ProfileWithLegacy(string? description = null, string? goal = null, string? career = null)
    {
        var p = EmptyProfile();
        // SetInitialProfile sets LearningGoal and CareerContext
        if (goal is not null || career is not null)
            p.SetInitialProfile(null, null, null, career, goal, null, null, null);
        // LearningGoalDescription has no public setter; use reflection for legacy test coverage
        if (description is not null)
        {
            var prop = typeof(StudentProfile).GetProperty("LearningGoalDescription",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            prop!.SetValue(p, description);
        }
        return p;
    }

    private static StudentProfile ProfileWithSupportLanguage(string code, string name)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, code, name, null, null, null, null, null, null, null);
        return p;
    }

    private static StudentProfile ProfileWithDifficulty(DifficultyPreference difficulty)
    {
        var p = EmptyProfile();
        p.UpdateLearningPreferences(null, null, null, null, null, null, null, null, difficulty, null);
        return p;
    }

    // ── test 1: explicit override used first ───────────────────────────────

    [Fact]
    public void Resolve_ExplicitOverride_UsedFirst()
    {
        var profile = ProfileWithGoals("improve speaking");
        var ctx = new LearningGoalResolutionContext { ExplicitGoalOverride = "exam prep" };

        var result = _sut.Resolve(profile, ctx);

        result.ContextSummary.Should().Be("exam prep");
        result.Source.Should().Be("Explicit");
        result.LegacyFallbackUsed.Should().BeFalse();
    }

    // ── test 2: LearningGoals from profile used ────────────────────────────

    [Fact]
    public void Resolve_LearningGoals_IncludedInSummary()
    {
        var profile = ProfileWithGoals("improve speaking", "pass IELTS");

        var result = _sut.Resolve(profile);

        result.Source.Should().Be("Structured");
        result.GoalLabels.Should().Contain("improve speaking").And.Contain("pass IELTS");
        result.ContextSummary.Should().Contain("improve speaking");
        result.LegacyFallbackUsed.Should().BeFalse();
        result.PrimaryGoalKey.Should().Be("improve speaking");
    }

    // ── test 3: CustomLearningGoal included ───────────────────────────────

    [Fact]
    public void Resolve_CustomGoal_IncludedInSummary()
    {
        var profile = ProfileWithCustomGoal("help my kids with homework");

        var result = _sut.Resolve(profile);

        result.Source.Should().Be("Structured");
        result.CustomGoal.Should().Be("help my kids with homework");
        result.ContextSummary.Should().Contain("help my kids with homework");
    }

    // ── test 4: legacy fallback used when v2 fields empty ─────────────────

    [Fact]
    public void Resolve_LegacyFields_UsedWhenStructuredEmpty()
    {
        var profile = ProfileWithLegacy(description: "I want to speak fluently at work");

        var result = _sut.Resolve(profile);

        result.Source.Should().Be("Legacy");
        result.LegacyFallbackUsed.Should().BeTrue();
        result.ContextSummary.Should().Be("I want to speak fluently at work");
    }

    [Fact]
    public void Resolve_LegacyGoalField_UsedWhenDescriptionMissing()
    {
        var profile = ProfileWithLegacy(goal: "travel English");

        var result = _sut.Resolve(profile);

        result.Source.Should().Be("Legacy");
        result.LegacyFallbackUsed.Should().BeTrue();
        result.ContextSummary.Should().Be("travel English");
    }

    [Fact]
    public void Resolve_CareerContext_UsedAsLastLegacyFallback()
    {
        var profile = ProfileWithLegacy(career: "Software engineer");

        var result = _sut.Resolve(profile);

        result.Source.Should().Be("Legacy");
        result.LegacyFallbackUsed.Should().BeTrue();
        result.ContextSummary.Should().Be("Software engineer");
    }

    // ── test 5: generic fallback when all empty ───────────────────────────

    [Fact]
    public void Resolve_AllFieldsEmpty_ReturnsGenericFallback()
    {
        var profile = EmptyProfile();

        var result = _sut.Resolve(profile);

        result.Source.Should().Be("Fallback");
        result.ContextSummary.Should().Be("general English communication");
        result.LegacyFallbackUsed.Should().BeFalse();
    }

    // ── test 6: WorkplaceSpecific = true for workplace goals ─────────────

    [Fact]
    public void Resolve_WorkplaceGoal_SetsWorkplaceSpecificTrue()
    {
        var profile = ProfileWithGoals("professional English for business meetings");

        var result = _sut.Resolve(profile);

        result.WorkplaceSpecific.Should().BeTrue();
    }

    [Fact]
    public void Resolve_CareerGoal_SetsWorkplaceSpecificTrue()
    {
        var profile = ProfileWithLegacy(career: "advance my career in finance");

        var result = _sut.Resolve(profile);

        result.WorkplaceSpecific.Should().BeTrue();
    }

    // ── test 7: WorkplaceSpecific = false for day-to-day goals ───────────

    [Fact]
    public void Resolve_TravelGoal_WorkplaceSpecificFalse()
    {
        var profile = ProfileWithGoals("travel and meet new people");

        var result = _sut.Resolve(profile);

        result.WorkplaceSpecific.Should().BeFalse();
    }

    [Fact]
    public void Resolve_GenericFallback_WorkplaceSpecificFalse()
    {
        var result = _sut.Resolve(EmptyProfile());

        result.WorkplaceSpecific.Should().BeFalse();
    }

    // ── test 8: SupportLanguage and DifficultyPreference included ─────────

    [Fact]
    public void Resolve_SupportLanguage_IncludedInResult()
    {
        var profile = ProfileWithSupportLanguage("fa", "Persian");

        var result = _sut.Resolve(profile);

        result.SupportLanguageCode.Should().Be("fa");
        result.SupportLanguageName.Should().Be("Persian");
    }

    [Fact]
    public void Resolve_DifficultyPreference_IncludedInResult()
    {
        var profile = ProfileWithDifficulty(DifficultyPreference.Challenging);

        var result = _sut.Resolve(profile);

        result.DifficultyPreference.Should().Be("challenging");
    }

    // ── test 9: missing profile values do not throw ───────────────────────

    [Fact]
    public void Resolve_NullContext_DoesNotThrow()
    {
        var profile = EmptyProfile();
        var act = () => _sut.Resolve(profile, null);
        act.Should().NotThrow();
    }

    [Fact]
    public void Resolve_NullProfile_ThrowsArgumentNullException()
    {
        var act = () => _sut.Resolve(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── test 10: LegacyFallbackUsed = true only from legacy source ────────

    [Fact]
    public void Resolve_StructuredSource_LegacyFallbackUsedFalse()
    {
        var profile = ProfileWithGoals("improve reading");

        var result = _sut.Resolve(profile);

        result.LegacyFallbackUsed.Should().BeFalse();
    }

    [Fact]
    public void Resolve_FocusAreasOnly_SourceStructured_LegacyFalse()
    {
        var profile = ProfileWithFocusAreas("grammar", "pronunciation");

        var result = _sut.Resolve(profile);

        result.Source.Should().Be("Structured");
        result.LegacyFallbackUsed.Should().BeFalse();
        result.FocusAreaLabels.Should().Contain("grammar").And.Contain("pronunciation");
    }

    // ── test 11: context summary bounded at 200 chars ─────────────────────

    [Fact]
    public void Resolve_VeryLongGoal_SummaryTruncatedAt200()
    {
        var longGoal = new string('a', 250);
        var profile = ProfileWithGoals(longGoal[..100]); // max 100 per goal

        var result = _sut.Resolve(profile);

        result.ContextSummary.Length.Should().BeLessOrEqualTo(200);
    }
}
