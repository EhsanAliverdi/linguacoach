using LinguaCoach.Application.Learning;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Learning;
using Xunit;

namespace LinguaCoach.IntegrationTests.Learning;

/// <summary>
/// Integration-level tests for LearningGoalContextResolver.
/// Verify end-to-end resolution using realistic StudentProfile states.
/// No database required — resolver is a pure computation service.
/// </summary>
public sealed class LearningGoalContextResolverIntegrationTests
{
    private readonly LearningGoalContextResolver _resolver = new();

    [Fact]
    public void Resolve_ProfileWithStructuredGoals_ReturnsThem()
    {
        var profile = new StudentProfile(Guid.NewGuid());
        profile.UpdateLearningPreferences(
            preferredName: "Ali",
            supportLanguageCode: "fa",
            supportLanguageName: "Persian",
            translationHelpPreference: null,
            learningGoals: ["improve speaking", "pass IELTS"],
            customLearningGoal: null,
            focusAreas: ["pronunciation"],
            customFocusArea: null,
            difficultyPreference: DifficultyPreference.Balanced,
            preferredSessionDurationMinutes: null);

        var result = _resolver.Resolve(profile, new LearningGoalResolutionContext { Source = "IntegrationTest" });

        Assert.Equal("Structured", result.Source);
        Assert.Contains("improve speaking", result.GoalLabels);
        Assert.Contains("pass IELTS", result.GoalLabels);
        Assert.Contains("pronunciation", result.FocusAreaLabels);
        Assert.Equal("fa", result.SupportLanguageCode);
        Assert.Equal("Persian", result.SupportLanguageName);
        Assert.Equal("balanced", result.DifficultyPreference);
        Assert.False(result.LegacyFallbackUsed);
        Assert.True(result.ContextSummary.Length <= 200);
    }

    [Fact]
    public void Resolve_EmptyProfile_GenericFallbackIsNotWorkplaceBiased()
    {
        var profile = new StudentProfile(Guid.NewGuid());

        var result = _resolver.Resolve(profile);

        Assert.Equal("Fallback", result.Source);
        Assert.False(result.WorkplaceSpecific);
        Assert.DoesNotContain("workplace", result.ContextSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("professional", result.ContextSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("business", result.ContextSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("career", result.ContextSummary, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.LegacyFallbackUsed);
    }
}
