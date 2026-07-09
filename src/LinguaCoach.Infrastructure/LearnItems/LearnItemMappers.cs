using LinguaCoach.Application.LearnItems;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.LearnItems;

internal static class LearnItemMappers
{
    public static LearnItemDto ToDto(LearnItem item, IReadOnlyList<LearnItemResourceLink> links) => new(
        item.Id, item.Title, item.Body, item.ExamplesJson, item.CommonMistakesJson, item.UsageNotes,
        item.CefrLevel, item.Skill, item.Subskill, item.ContextTagsJson, item.FocusTagsJson,
        item.DifficultyBand, item.EstimatedMinutes, item.SourceMode.ToString(), item.GenerationProvider,
        item.GenerationModel, item.ReviewStatus.ToString(), item.CreatedByUserId, item.ReviewedByUserId,
        item.ApprovedAtUtc, item.RejectedAtUtc, item.RejectionReason, item.ReviewNotes,
        item.CreatedAt, item.UpdatedAtUtc,
        links.Select(ToLinkDto).ToList());

    public static LearnItemResourceLinkDto ToLinkDto(LearnItemResourceLink link) => new(
        link.Id, link.ResourceType.ToString(), link.ResourceId, link.Role.ToString(),
        link.SnapshotTitle, link.ContentFingerprint);
}
