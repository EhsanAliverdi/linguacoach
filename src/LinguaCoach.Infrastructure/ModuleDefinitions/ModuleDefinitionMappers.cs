using LinguaCoach.Application.ModuleDefinitions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.ModuleDefinitions;

internal static class ModuleDefinitionMappers
{
    public static ModuleDefinitionDto ToDto(
        ModuleDefinition item,
        IReadOnlyList<ModuleDefinitionLearnItemLink> learnItemLinks,
        IReadOnlyList<ModuleDefinitionActivityLink> activityLinks) => new(
        item.Id, item.Title, item.Description, item.ObjectiveKey, item.CefrLevel, item.Skill, item.Subskill,
        item.ContextTagsJson, item.FocusTagsJson, item.DifficultyBand, item.EstimatedMinutes, item.FeedbackPlanJson,
        item.SourceMode.ToString(), item.GenerationProvider, item.GenerationModel, item.ReviewStatus.ToString(),
        item.CreatedByUserId, item.ReviewedByUserId, item.ApprovedAtUtc, item.RejectedAtUtc,
        item.RejectionReason, item.ReviewNotes, item.CreatedAt, item.UpdatedAtUtc,
        learnItemLinks.OrderBy(l => l.SortOrder).Select(ToLearnItemLinkDto).ToList(),
        activityLinks.OrderBy(l => l.SortOrder).Select(ToActivityLinkDto).ToList());

    public static ModuleLearnItemLinkDto ToLearnItemLinkDto(ModuleDefinitionLearnItemLink link) => new(
        link.Id, link.LearnItemId, link.Role.ToString(), link.SortOrder, link.SnapshotTitle);

    public static ModuleActivityLinkDto ToActivityLinkDto(ModuleDefinitionActivityLink link) => new(
        link.Id, link.ActivityDefinitionId, link.Role.ToString(), link.SortOrder, link.Required, link.SnapshotTitle);
}
