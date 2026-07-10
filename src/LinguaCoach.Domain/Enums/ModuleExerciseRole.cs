namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H5 — a <see cref="Entities.ModuleExerciseLink"/>'s role within its
/// Module. Distinct from <see cref="LessonResourceRole"/> (Primary/Supporting, used for the
/// Lesson link) because a Module's practice activities have a richer set of roles than a
/// single source resource link does.</summary>
public enum ModuleExerciseRole
{
    PrimaryPractice = 0,
    SupportingPractice = 1,
    Review = 2,
    Extension = 3
}
