using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H10 — a traceability bookkeeping record of every time an approved
/// <see cref="ActivityDefinition"/> (reached via an approved <see cref="ModuleDefinition"/>
/// suggestion) was launched into a real, runnable <see cref="LearningActivity"/>. This is the
/// bridge record H10's decision review calls for: it never carries answer/score data itself —
/// scoring happens through the existing <see cref="LearningActivity"/>/<see cref="ActivityAttempt"/>
/// pipeline unchanged — it exists purely so admin diagnostics and future audits can answer
/// "which ModuleDefinition/ActivityDefinition/LearnItem did this LearningActivity come from"
/// without guessing. Mirrors <see cref="StudentPracticeGymModuleAssignment"/> (H7) and
/// <see cref="StudentDailyModuleAssignment"/> (H6)'s additive-bookkeeping convention.
/// </summary>
public sealed class StudentActivityDefinitionLaunch : BaseEntity
{
    public Guid StudentId { get; private set; }
    public Guid ModuleDefinitionId { get; private set; }
    public Guid ActivityDefinitionId { get; private set; }

    /// <summary>Null when the launched Module has no linked (or no Approved) Learn Item.</summary>
    public Guid? LearnItemId { get; private set; }

    /// <summary>The real, runnable <see cref="LearningActivity"/> this launch materialized —
    /// the entity the existing submission/scoring/ledger pipeline already understands.</summary>
    public Guid LearningActivityId { get; private set; }

    public ActivityDefinitionLaunchSource Source { get; private set; }
    public DateTimeOffset LaunchedAt { get; private set; }

    private StudentActivityDefinitionLaunch() { }

    public StudentActivityDefinitionLaunch(
        Guid studentId,
        Guid moduleDefinitionId,
        Guid activityDefinitionId,
        Guid learningActivityId,
        ActivityDefinitionLaunchSource source,
        DateTimeOffset launchedAt,
        Guid? learnItemId = null)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId must not be empty.", nameof(studentId));
        if (moduleDefinitionId == Guid.Empty)
            throw new ArgumentException("ModuleDefinitionId must not be empty.", nameof(moduleDefinitionId));
        if (activityDefinitionId == Guid.Empty)
            throw new ArgumentException("ActivityDefinitionId must not be empty.", nameof(activityDefinitionId));
        if (learningActivityId == Guid.Empty)
            throw new ArgumentException("LearningActivityId must not be empty.", nameof(learningActivityId));

        StudentId = studentId;
        ModuleDefinitionId = moduleDefinitionId;
        ActivityDefinitionId = activityDefinitionId;
        LearnItemId = learnItemId;
        LearningActivityId = learningActivityId;
        Source = source;
        LaunchedAt = launchedAt;
    }
}
