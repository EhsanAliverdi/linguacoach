namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H4 — how an <see cref="Entities.Exercise"/>'s initial draft content
/// came to exist.
///
/// Phase 2 (2026-07-15 exercise pipeline boundary consolidation) — removed
/// <c>GeneratedFromResources</c>: direct Resource-to-Exercise generation with no Lesson context
/// no longer exists (every Exercise requires a Lesson), so every Exercise generated
/// deterministically or via AI is unambiguously <see cref="GeneratedFromLesson"/>. See migration
/// <c>Phase_2_ConvertGeneratedFromResourcesSourceMode</c> for the data conversion of any existing
/// row that still had the old value.</summary>
public enum ExerciseSourceMode
{
    Manual = 0,
    GeneratedFromLesson = 2,
    Imported = 3
}
