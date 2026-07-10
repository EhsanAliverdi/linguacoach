using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H10 — a traceability bookkeeping record of every time an approved
/// <see cref="Exercise"/> (reached via an approved <see cref="Module"/>
/// suggestion) was launched into a real, runnable <see cref="LearningActivity"/>. This is the
/// bridge record H10's decision review calls for: it never carries answer/score data itself —
/// scoring happens through the existing <see cref="LearningActivity"/>/<see cref="ActivityAttempt"/>
/// pipeline unchanged — it exists purely so admin diagnostics and future audits can answer
/// "which Module/Exercise/Lesson did this LearningActivity come from"
/// without guessing. Mirrors <see cref="StudentPracticeGymModuleAssignment"/> (H7) and
/// <see cref="StudentDailyModuleAssignment"/> (H6)'s additive-bookkeeping convention.
/// </summary>
public sealed class StudentExerciseLaunch : BaseEntity
{
    public Guid StudentId { get; private set; }
    public Guid ModuleId { get; private set; }
    public Guid ExerciseId { get; private set; }

    /// <summary>Null when the launched Module has no linked (or no Approved) Lesson.</summary>
    public Guid? LessonId { get; private set; }

    /// <summary>The real, runnable <see cref="LearningActivity"/> this launch materialized —
    /// the entity the existing submission/scoring/ledger pipeline already understands.</summary>
    public Guid LearningActivityId { get; private set; }

    public ExerciseLaunchSource Source { get; private set; }
    public DateTimeOffset LaunchedAt { get; private set; }

    private StudentExerciseLaunch() { }

    public StudentExerciseLaunch(
        Guid studentId,
        Guid moduleId,
        Guid exerciseId,
        Guid learningActivityId,
        ExerciseLaunchSource source,
        DateTimeOffset launchedAt,
        Guid? lessonId = null)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId must not be empty.", nameof(studentId));
        if (moduleId == Guid.Empty)
            throw new ArgumentException("ModuleId must not be empty.", nameof(moduleId));
        if (exerciseId == Guid.Empty)
            throw new ArgumentException("ExerciseId must not be empty.", nameof(exerciseId));
        if (learningActivityId == Guid.Empty)
            throw new ArgumentException("LearningActivityId must not be empty.", nameof(learningActivityId));

        StudentId = studentId;
        ModuleId = moduleId;
        ExerciseId = exerciseId;
        LessonId = lessonId;
        LearningActivityId = learningActivityId;
        Source = source;
        LaunchedAt = launchedAt;
    }
}
