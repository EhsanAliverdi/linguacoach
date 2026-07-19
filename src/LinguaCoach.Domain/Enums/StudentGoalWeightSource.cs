namespace LinguaCoach.Domain.Enums;

/// <summary>Adaptive Curriculum Sprint 3 — which mechanism most recently updated a
/// <see cref="Entities.StudentGoalWeight"/> row. Informational only (both explicit and implicit
/// updates write to the same <c>Weight</c> value) — lets the "My Goals" UI show the student which
/// of their goals they set directly vs. which the app inferred from their activity.</summary>
public enum StudentGoalWeightSource
{
    Explicit = 0,
    Implicit = 1
}
