namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H3 — how a <see cref="Entities.Lesson"/>'s initial draft content came to exist.</summary>
public enum LessonSourceMode
{
    Manual = 0,
    GeneratedFromResources = 1,
    Imported = 2
}
