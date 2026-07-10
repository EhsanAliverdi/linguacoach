using LinguaCoach.Application.Lessons;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.Lessons;

internal static class LessonMappers
{
    public static LessonDto ToDto(Lesson item, IReadOnlyList<LessonResourceLink> links) => new(
        item.Id, item.Title, item.Body, item.ExamplesJson, item.CommonMistakesJson, item.UsageNotes,
        item.CefrLevel, item.Skill, item.Subskill, item.ContextTagsJson, item.FocusTagsJson,
        item.DifficultyBand, item.EstimatedMinutes, item.SourceMode.ToString(), item.GenerationProvider,
        item.GenerationModel, item.ReviewStatus.ToString(), item.CreatedByUserId, item.ReviewedByUserId,
        item.ApprovedAtUtc, item.RejectedAtUtc, item.RejectionReason, item.ReviewNotes,
        item.CreatedAt, item.UpdatedAtUtc,
        links.Select(ToLinkDto).ToList());

    public static LessonResourceLinkDto ToLinkDto(LessonResourceLink link) => new(
        link.Id, link.ResourceType.ToString(), link.ResourceId, link.Role.ToString(),
        link.SnapshotTitle, link.ContentFingerprint);
}
