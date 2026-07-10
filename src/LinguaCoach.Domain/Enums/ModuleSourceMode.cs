namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H5 — how a <see cref="Entities.Module"/>'s initial draft came to
/// exist. Mirrors <see cref="ExerciseSourceMode"/>'s shape.</summary>
public enum ModuleSourceMode
{
    Manual = 0,
    GeneratedFromLessonAndExercises = 1,
    GeneratedFromResources = 2,
    Imported = 3
}
