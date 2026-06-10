namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Determines how a student's submitted answer is evaluated.
/// Stored as integer in exercise_patterns.marking_mode.
/// Never reorder or insert — append only.
/// </summary>
public enum MarkingMode
{
    AiOpenEnded    = 0,  // AI evaluates free text holistically (no rubric)
    AiStructured   = 1,  // AI evaluates against a rubric defined in the pattern's eval prompt
    ExactMatch     = 2,  // Answer checked against acceptedAnswers list (deterministic, no AI call)
    KeyedSelection = 3,  // Selection checked against correctIndex/correctId (deterministic)
    NoMarking      = 4,  // Read-only step — no submission required, no evaluation
}
