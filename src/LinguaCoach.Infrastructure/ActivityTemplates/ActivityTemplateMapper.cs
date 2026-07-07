using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

internal static class ActivityTemplateMapper
{
    public static AdminActivityTemplateDto ToDto(ActivityTemplate t) => new(
        t.Id, t.Key, t.VersionNumber, t.PreviousVersionId,
        t.Skill, t.Subskill, t.CefrLevel,
        t.ContextTagsJson, t.FocusTagsJson, t.CurriculumObjectiveKey,
        t.ActivityType, t.PatternKey,
        t.FormIoBaseSchemaJson, t.GenerationInstructions, t.ScoringModelJson, t.ValidationRulesJson,
        t.ReviewStatus.ToString(), t.IsPublished,
        t.EstimatedDurationSeconds, t.AssetRequirementsJson);
}
