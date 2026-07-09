using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ActivityDefinitionLaunch;

/// <summary>Phase H10 — request to launch a real, runnable practice attempt from an approved
/// Module's approved, launch-eligible Activity Definition.</summary>
public sealed record ActivityDefinitionLaunchRequest(
    Guid StudentId,
    Guid ModuleDefinitionId,
    ActivityDefinitionLaunchSource Source);

/// <summary>Phase H10 — student-safe launch result. When <see cref="Success"/> is false,
/// <see cref="UnsupportedReason"/> explains why and every other field is null — the caller must
/// fall back to the existing display-only/Practice Gym behavior. Never exposes
/// <c>AnswerKeyJson</c>/<c>ScoringRulesJson</c>/admin review internals.</summary>
public sealed record ActivityDefinitionLaunchResult(
    bool Success,
    string? UnsupportedReason,
    Guid ModuleDefinitionId,
    Guid? ActivityDefinitionId,
    /// <summary>The real, runnable <see cref="Domain.Entities.LearningActivity"/> id — navigate the
    /// student to the existing <c>/activity?activityId=...</c> page with this id; submission uses
    /// the existing, unchanged <c>POST api/activity/{activityId}/attempt</c> endpoint.</summary>
    Guid? LearningActivityId,
    string? Title,
    string? Instructions,
    string? RendererType,
    string? FormSchemaJson,
    int? EstimatedMinutes,
    string? Skill,
    string? Subskill,
    string? CefrLevel,
    /// <summary>True once launched — submission uses the existing, unchanged activity-attempt
    /// pipeline, so this is always true when <see cref="Success"/> is true.</summary>
    bool CanSubmit,
    PracticeGymModuleLearnItemSummary? LearnItem);

public interface IActivityDefinitionLaunchService
{
    /// <summary>Re-validates eligibility fresh (approval/content can change between suggestion
    /// and click) and, only if eligible, materializes the Activity Definition into a real
    /// <see cref="Domain.Entities.LearningActivity"/> — the same mechanism the existing
    /// ActivityTemplate Form.io pilot already uses — then records a traceability bridge row.
    /// Never throws for "not launchable" — returns <see cref="ActivityDefinitionLaunchResult.Success"/>
    /// = false instead, and the existing Practice Gym/Today fallback remains available either
    /// way.</summary>
    Task<ActivityDefinitionLaunchResult> LaunchAsync(
        ActivityDefinitionLaunchRequest request, CancellationToken ct = default);
}
