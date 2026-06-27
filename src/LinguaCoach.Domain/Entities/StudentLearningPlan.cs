using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/*
 * Learning Plan lifecycle:
 *
 *   Active ──InvalidateThenRegenerate()──► Regenerating ──MarkReady()──► Active
 *
 * A student has at most one Active or Regenerating plan at a time.
 * Old plans are marked Superseded when a new plan is activated.
 *
 * The plan is the orchestration layer that coordinates CurriculumRouting,
 * StudentMasteryEvaluation, and ReadinessPool — it does not replace them.
 */

/// <summary>
/// Represents a student's personalized learning plan.
/// Determines objective sequencing, lesson queue, and review schedule.
/// Deterministic — no direct AI calls.
/// </summary>
public sealed class StudentLearningPlan : BaseEntity
{
    public Guid StudentProfileId { get; private set; }

    /// <summary>CEFR level at the time this plan was generated.</summary>
    public string CefrLevelSnapshot { get; private set; }

    public LearningPlanStatus Status { get; private set; }

    /// <summary>Why this plan was generated or last regenerated.</summary>
    public string RegenerationReason { get; private set; }

    /// <summary>Number of times this plan has been regenerated.</summary>
    public int RegenerationCount { get; private set; }

    /// <summary>UTC when the plan was last fully evaluated.</summary>
    public DateTime? LastEvaluatedAt { get; private set; }

    /// <summary>Configurable number of planned lessons maintained in the queue.</summary>
    public int PlannedLessonCount { get; private set; }

    public ICollection<StudentLearningPlanObjective> Objectives { get; private set; } = [];

    private StudentLearningPlan() { }

    public StudentLearningPlan(
        Guid studentProfileId,
        string cefrLevelSnapshot,
        string regenerationReason,
        int plannedLessonCount = 10)
    {
        StudentProfileId = studentProfileId;
        CefrLevelSnapshot = cefrLevelSnapshot;
        RegenerationReason = regenerationReason;
        PlannedLessonCount = plannedLessonCount;
        Status = LearningPlanStatus.Active;
        Objectives = [];
    }

    public void MarkReady(DateTime evaluatedAt)
    {
        Status = LearningPlanStatus.Active;
        LastEvaluatedAt = evaluatedAt;
    }

    public void Supersede()
    {
        Status = LearningPlanStatus.Superseded;
    }

    public void StartRegeneration(string reason)
    {
        Status = LearningPlanStatus.Regenerating;
        RegenerationReason = reason;
        RegenerationCount++;
    }

    public void AddObjective(StudentLearningPlanObjective objective)
    {
        Objectives.Add(objective);
    }

    public void ClearObjectives()
    {
        Objectives.Clear();
    }
}
