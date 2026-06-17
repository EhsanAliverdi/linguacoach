using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.Curriculum;

/// <summary>
/// Builds a CurriculumRoutingRequest from a StudentProfile and resolved goal context.
/// Centralises the mapping so all generation handlers use the same fields.
/// </summary>
public static class CurriculumRoutingRequestFactory
{
    public static CurriculumRoutingRequest Build(
        StudentProfile profile,
        ResolvedLearningGoalContext resolvedGoalContext,
        string source,
        string? primarySkill = null,
        string? requestedPatternKey = null,
        bool allowReviewOrScaffold = false,
        IReadOnlyList<string>? recentWeakPatternKeys = null)
    {
        return new CurriculumRoutingRequest
        {
            StudentId = profile.Id,
            CurrentCefrLevel = profile.CefrLevel,
            PrimarySkill = primarySkill,
            RequestedPatternKey = requestedPatternKey,
            Source = source,
            ResolvedLearningGoalContext = resolvedGoalContext,
            LearningGoals = profile.LearningGoals,
            CustomLearningGoal = profile.CustomLearningGoal,
            FocusAreas = profile.FocusAreas,
            CustomFocusArea = profile.CustomFocusArea,
            DifficultyPreference = profile.DifficultyPreference?.ToString(),
            PreferredSessionDurationMinutes = profile.PreferredSessionDurationMinutes,
            SupportLanguageCode = profile.SupportLanguageCode,
            SupportLanguageName = profile.SupportLanguageName,
            AllowReviewOrScaffold = allowReviewOrScaffold,
            RecentWeakPatternKeys = recentWeakPatternKeys ?? []
        };
    }
}
