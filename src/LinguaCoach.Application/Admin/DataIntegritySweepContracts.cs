namespace LinguaCoach.Application.Admin;

/// <summary>
/// Sprint 11 — one unified admin sweep combining two previously-scattered checks: (1) real
/// orphan/FK checks across <c>StudentLearningPlan</c>/<c>StudentLearningPlanObjective</c>/
/// <c>ActivityAttempt</c>/<c>StudentExerciseLaunch</c> (a category of check that didn't exist
/// anywhere in admin before this — the DB's own RESTRICT foreign keys already structurally
/// prevent most of this corruption class, so these checks are expected to stay at zero; their
/// value is proving that stays true, not fixing an existing problem), and (2) the existing
/// per-entity content-completeness issue counts (Module/Lesson/Exercise/Resource Bank) that
/// already existed as separate, unconnected admin pages. The existing <c>/admin/diagnostics</c>
/// page (AI-generation health — failed/stuck generation jobs, provider status) is a deliberately
/// distinct concern, not folded in here — its own page heading was clarified in the same Sprint 11
/// pass to make that scope explicit, since "Diagnostics" on its own read as if it already covered
/// data integrity.
/// </summary>
public interface IDataIntegritySweepService
{
    Task<DataIntegritySweepResult> RunAsync(CancellationToken ct = default);
}

public sealed record DataIntegritySweepResult(
    DateTimeOffset RanAtUtc,
    IReadOnlyList<DataIntegrityCategoryResult> Categories)
{
    public bool AllHealthy => Categories.All(c => c.Healthy);
}

public sealed record DataIntegrityCategoryResult(
    string Category,
    string Description,
    int TotalChecked,
    int IssuesFound,
    bool Healthy);
