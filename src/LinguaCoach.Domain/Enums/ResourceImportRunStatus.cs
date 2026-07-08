namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.ResourceImportRun"/> (Phase E1 — English Resource
/// Import Staging). See docs/reviews for the Phase E0/E1 pipeline plan.
/// </summary>
public enum ResourceImportRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    CompletedWithWarnings = 3,
    Failed = 4,
    Cancelled = 5
}
