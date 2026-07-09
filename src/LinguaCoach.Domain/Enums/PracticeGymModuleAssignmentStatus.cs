namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H7 — lifecycle of a <see cref="Entities.StudentPracticeGymModuleAssignment"/>. A
/// lightweight bookkeeping/diagnostic record, not a student attempt/score record — no scoring or
/// mastery state is tracked here. Mirrors <see cref="DailyModuleAssignmentStatus"/>'s convention,
/// adapted for Practice Gym's suggest-then-optionally-select flow (Today's Daily Lesson pipeline
/// auto-assigns; Practice Gym only ever suggests).</summary>
public enum PracticeGymModuleAssignmentStatus
{
    /// <summary>Chosen by the deterministic selector and recorded for this student.</summary>
    Suggested = 0,

    /// <summary>Shown to the student on the Practice Gym page.</summary>
    Presented = 1,

    /// <summary>The student selected this suggestion (not implemented as a student action in H7 —
    /// reserved for a future phase; H7 never sets this).</summary>
    Selected = 2,

    /// <summary>The student explicitly dismissed it (not implemented as a student action in H7 —
    /// reserved for a future phase).</summary>
    Dismissed = 3,

    /// <summary>The student engaged with it (not implemented as a student action in H7 —
    /// reserved for a future phase; H7 never sets this).</summary>
    Consumed = 4,

    /// <summary>Suggestion has aged out of relevance.</summary>
    Expired = 5,

    /// <summary>No suitable Module existed — legacy Practice Gym suggestions were used instead.
    /// Recorded for admin diagnostics so "did Practice Gym use the module pipeline" is answerable
    /// without re-running selection.</summary>
    FallbackOnly = 6
}
