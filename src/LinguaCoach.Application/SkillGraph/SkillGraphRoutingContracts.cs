namespace LinguaCoach.Application.SkillGraph;

/// <summary>
/// Adaptive Curriculum Sprint 7 — replaces <c>ICurriculumRoutingService</c>/<c>CurriculumObjective</c>
/// as the source <c>LearningPlanService.BuildObjectiveSequenceAsync</c> routes plan objectives
/// against. Candidates are <c>SkillGraphNode</c> rows rather than <c>CurriculumObjective</c> rows,
/// and — unlike the legacy router, which never checked whether an objective had any real runnable
/// content behind it — this service prefers a node with at least one linked, eligible
/// (<c>ModuleEligibility.AvailableForNewStudentDeliveryExpr</c>) Module over one without, so a
/// recommended objective is more likely to be genuinely actionable. <c>PreferredObjectiveKey</c>
/// safety-checking (the legacy router's Rule-5-adjacent "prefer this key if valid" path) is not
/// carried over — <c>LearningPlanService</c> never used it, and its only other caller
/// (<c>AdminCurriculumController</c>'s routing-preview diagnostic) is retired in this same sprint.
/// </summary>
public interface ISkillGraphRoutingService
{
    string NormalizeCefrLevel(string? rawLevel);

    Task<SkillGraphRoutingRecommendation> RecommendAsync(
        SkillGraphRoutingRequest request, CancellationToken ct = default);
}

public sealed record SkillGraphRoutingRequest(
    Guid StudentId,
    string? CurrentCefrLevel,
    string Source,
    Learning.ResolvedLearningGoalContext ResolvedLearningGoalContext,
    string? PrimarySkill = null,
    IReadOnlyList<string>? FocusAreas = null,
    string? CustomFocusArea = null,
    string? DifficultyPreference = null,
    /// <summary>When true and no exact-CEFR-level node candidate exists, falls back to one CEFR
    /// level down (mirrors the legacy router's review/scaffold behavior) rather than going
    /// straight to <see cref="SkillGraphRoutingReason.Fallback"/>.</summary>
    bool AllowReviewOrScaffold = false);

public enum SkillGraphRoutingReason
{
    /// <summary>Exact-CEFR-level node match, taxonomy-only (no linked content found).</summary>
    Normal,
    /// <summary>Exact-CEFR-level node match that also has at least one real, eligible linked
    /// Module — the preferred outcome.</summary>
    ContentBacked,
    /// <summary>One CEFR level below the student's level, per
    /// <see cref="SkillGraphRoutingRequest.AllowReviewOrScaffold"/>.</summary>
    Review,
    /// <summary>No node candidate found at the exact or (if allowed) one-level-down CEFR level —
    /// <see cref="SkillGraphRoutingRecommendation.NodeKey"/> is null.</summary>
    Fallback
}

public sealed record SkillGraphRoutingRecommendation(
    string TargetCefrLevel,
    string? PrimarySkill,
    string? NodeKey,
    string? NodeTitle,
    IReadOnlyList<string> ContextTags,
    int DifficultyBand,
    SkillGraphRoutingReason RoutingReason,
    bool IsLowerLevelContent,
    string Source,
    string Explanation);
