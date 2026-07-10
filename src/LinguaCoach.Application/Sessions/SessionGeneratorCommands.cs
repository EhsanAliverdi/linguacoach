namespace LinguaCoach.Application.Sessions;

// ── Result ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Summary of Today returned to callers. Phase I2B — Today is module-only: the legacy
/// LearningSession/SessionExercise generation pipeline (ISessionGeneratorService/
/// SessionGeneratorService) was deleted, so this record no longer carries per-exercise session
/// content. <see cref="Available"/> is the single honest signal of whether there is anything to
/// show today; when false, <see cref="TodayPlan"/> is null (or its
/// <c>FallbackRequired</c>/empty-selection state) and the caller must render a clear "nothing
/// available yet" state — never a silently-empty legacy shape. See
/// docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md and
/// docs/reviews/2026-07-10-phase-i4-pass3-today-plan-rename-review.md (field renamed from
/// <c>ModuleSection</c> to <c>TodayPlan</c>).
/// </summary>
public sealed record TodaysSessionResult(
    bool Available,
    TodayPlanModules.TodayPlanModuleSelectionResult? TodayPlan);
