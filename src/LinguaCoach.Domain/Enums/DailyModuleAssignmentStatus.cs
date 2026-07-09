namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H6 — lifecycle of a <see cref="Entities.StudentDailyModuleAssignment"/>. A
/// lightweight bookkeeping/diagnostic record, not a student attempt/score record — no scoring or
/// mastery state is tracked here.</summary>
public enum DailyModuleAssignmentStatus
{
    /// <summary>Chosen by the deterministic selector and recorded for this student/date.</summary>
    Selected = 0,

    /// <summary>Shown to the student on the Today page.</summary>
    Presented = 1,

    /// <summary>The student explicitly skipped it (not implemented as a student action in H6 —
    /// reserved for a future phase).</summary>
    Skipped = 2,

    /// <summary>The student engaged with it (not implemented as a student action in H6 —
    /// reserved for a future phase; H6 never sets this).</summary>
    Consumed = 3,

    /// <summary>Assignment date has passed without being presented.</summary>
    Expired = 4,

    /// <summary>No suitable Module existed — fallback to legacy Today content was used. Recorded
    /// for admin diagnostics so "did Today use the module pipeline today" is answerable without
    /// re-running selection.</summary>
    FallbackOnly = 5
}
