using LinguaCoach.Application.Modules;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.Modules;

internal static class ModuleMappers
{
    public static ModuleDto ToDto(
        Module item,
        IReadOnlyList<ModuleLessonLink> lessonLinks,
        IReadOnlyList<ModuleExerciseLink> exerciseLinks) => new(
        item.Id, item.Title, item.Description, item.ObjectiveKey, item.CefrLevel, item.Skill, item.Subskill,
        item.ContextTagsJson, item.FocusTagsJson, item.DifficultyBand, item.EstimatedMinutes, item.FeedbackPlanJson,
        item.SourceMode.ToString(), item.GenerationProvider, item.GenerationModel, item.ReviewStatus.ToString(),
        item.CreatedByUserId, item.ReviewedByUserId, item.ApprovedAtUtc, item.RejectedAtUtc,
        item.RejectionReason, item.ReviewNotes, item.CreatedAt, item.UpdatedAtUtc,
        lessonLinks.OrderBy(l => l.SortOrder).Select(ToLessonLinkDto).ToList(),
        exerciseLinks.OrderBy(l => l.SortOrder).Select(ToExerciseLinkDto).ToList(),
        item.IsArchived);

    public static ModuleLessonLinkDto ToLessonLinkDto(ModuleLessonLink link) => new(
        link.Id, link.LessonId, link.Role.ToString(), link.SortOrder, link.SnapshotTitle);

    public static ModuleExerciseLinkDto ToExerciseLinkDto(ModuleExerciseLink link) => new(
        link.Id, link.ExerciseId, link.Role.ToString(), link.SortOrder, link.Required, link.SnapshotTitle);
}
