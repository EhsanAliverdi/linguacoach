using LinguaCoach.Application.ActivityDefinitions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.ActivityDefinitions;

internal static class ActivityDefinitionMappers
{
    public static ActivityDefinitionDto ToDto(ActivityDefinition item, IReadOnlyList<ActivityResourceLink> links) => new(
        item.Id, item.Title, item.Description, item.Instructions, item.ActivityType, item.PatternKey,
        item.RendererType.ToString(), item.FormSchemaJson, item.AnswerKeyJson, item.ScoringRulesJson,
        item.FeedbackPlanJson, item.CefrLevel, item.Skill, item.Subskill, item.ContextTagsJson, item.FocusTagsJson,
        item.DifficultyBand, item.EstimatedMinutes, item.LearnItemId, item.SourceMode.ToString(),
        item.GenerationProvider, item.GenerationModel, item.ReviewStatus.ToString(), item.CreatedByUserId,
        item.ReviewedByUserId, item.ApprovedAtUtc, item.RejectedAtUtc, item.RejectionReason, item.ReviewNotes,
        item.CreatedAt, item.UpdatedAtUtc,
        links.Select(ToLinkDto).ToList());

    public static ActivityResourceLinkDto ToLinkDto(ActivityResourceLink link) => new(
        link.Id, link.ResourceType.ToString(), link.ResourceId, link.Role.ToString(),
        link.SnapshotTitle, link.ContentFingerprint);
}
