namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H4 — how an <see cref="Entities.Exercise"/>'s initial draft content
/// came to exist. Mirrors <see cref="LessonSourceMode"/>'s shape with one addition
/// (<see cref="GeneratedFromLesson"/>) since an Activity can also be generated starting from an
/// already-approved-or-draft Lesson rather than raw Resource Bank rows.</summary>
public enum ExerciseSourceMode
{
    Manual = 0,
    GeneratedFromResources = 1,
    GeneratedFromLesson = 2,
    Imported = 3
}
