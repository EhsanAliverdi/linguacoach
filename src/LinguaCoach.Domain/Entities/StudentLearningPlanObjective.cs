using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single curriculum objective within a student's learning plan.
/// Tracks sequencing, status, source, and the reason it appears in the plan.
/// </summary>
public sealed class StudentLearningPlanObjective : BaseEntity
{
    public Guid StudentLearningPlanId { get; private set; }

    /// <summary>Maps to CurriculumObjective.Key.</summary>
    public string ObjectiveKey { get; private set; }

    public string CefrLevel { get; private set; }

    /// <summary>Primary skill (speaking, listening, reading, writing, vocabulary, grammar).</summary>
    public string Skill { get; private set; }

    /// <summary>Learner context (workplace, general_english, travel, etc.).</summary>
    public string Context { get; private set; }

    /// <summary>Human-readable title from CurriculumObjective.</summary>
    public string? Title { get; private set; }

    /// <summary>Relative priority within the plan. Lower = higher priority.</summary>
    public int Priority { get; private set; }

    /// <summary>Why this objective was included (routing, review, prerequisite, etc.).</summary>
    public string Source { get; private set; }

    public LearningPlanObjectiveStatus Status { get; private set; }

    /// <summary>Planned position in the lesson queue. Null = not yet queued.</summary>
    public int? PlannedOrder { get; private set; }

    public bool IsReview { get; private set; }

    public bool IsBlocked { get; private set; }

    /// <summary>ObjectiveKey of the blocking prerequisite, when IsBlocked = true.</summary>
    public string? BlockedByObjectiveKey { get; private set; }

    public DateTime? LastEvaluatedAt { get; private set; }

    private StudentLearningPlanObjective() { }

    public StudentLearningPlanObjective(
        Guid planId,
        string objectiveKey,
        string cefrLevel,
        string skill,
        string context,
        string? title,
        int priority,
        string source,
        int? plannedOrder = null,
        bool isReview = false,
        bool isBlocked = false,
        string? blockedByObjectiveKey = null)
    {
        StudentLearningPlanId = planId;
        ObjectiveKey = objectiveKey;
        CefrLevel = cefrLevel;
        Skill = skill;
        Context = context;
        Title = title;
        Priority = priority;
        Source = source;
        PlannedOrder = plannedOrder;
        IsReview = isReview;
        IsBlocked = isBlocked;
        BlockedByObjectiveKey = blockedByObjectiveKey;
        Status = isBlocked ? LearningPlanObjectiveStatus.Blocked : LearningPlanObjectiveStatus.Active;
    }

    public void MarkCompleted()
    {
        Status = LearningPlanObjectiveStatus.Completed;
        LastEvaluatedAt = DateTime.UtcNow;
    }

    public void MarkMastered()
    {
        Status = LearningPlanObjectiveStatus.Mastered;
        LastEvaluatedAt = DateTime.UtcNow;
    }

    public void MarkDeferred()
    {
        Status = LearningPlanObjectiveStatus.Deferred;
        LastEvaluatedAt = DateTime.UtcNow;
    }

    public void Unblock()
    {
        IsBlocked = false;
        BlockedByObjectiveKey = null;
        Status = LearningPlanObjectiveStatus.Active;
        LastEvaluatedAt = DateTime.UtcNow;
    }

    public void SetPlannedOrder(int order)
    {
        PlannedOrder = order;
    }

    public void Evaluate(DateTime at)
    {
        LastEvaluatedAt = at;
    }
}
