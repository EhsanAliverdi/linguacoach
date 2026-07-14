using LinguaCoach.Application.ExerciseLaunch;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.Exercises;

internal static class ExerciseMappers
{
    public static ExerciseDto ToDto(Exercise item, IReadOnlyList<ExerciseResourceLink> links)
    {
        var eligibility = ExerciseLaunchEligibility.EvaluateContentSupport(item);
        return new(
            item.Id, item.Title, item.Description, item.Instructions, item.ActivityType, item.PatternKey,
            item.RendererType.ToString(), item.FormSchemaJson, item.AnswerKeyJson, item.ScoringRulesJson,
            item.FeedbackPlanJson, item.CefrLevel, item.Skill, item.Subskill, item.ContextTagsJson, item.FocusTagsJson,
            item.DifficultyBand, item.EstimatedMinutes, item.LessonId, item.SourceMode.ToString(),
            item.GenerationProvider, item.GenerationModel, item.ReviewStatus.ToString(), item.CreatedByUserId,
            item.ReviewedByUserId, item.ApprovedAtUtc, item.RejectedAtUtc, item.RejectionReason, item.ReviewNotes,
            item.CreatedAt, item.UpdatedAtUtc,
            links.Select(ToLinkDto).ToList(),
            eligibility.CanLaunch, eligibility.UnsupportedReason, item.IsArchived);
    }

    public static ExerciseResourceLinkDto ToLinkDto(ExerciseResourceLink link) => new(
        link.Id, link.ResourceType.ToString(), link.ResourceId, link.Role.ToString(),
        link.SnapshotTitle, link.ContentFingerprint);
}
