using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Adaptive Curriculum Sprint 3 — one entry in a student's weighted goal vector: how relevant a
/// given life-goal/motivation tag (from <see cref="CurriculumContextTagConstants.GoalTags"/>, e.g.
/// Work, Travel, DayToDay) currently is to that student. Explicit input (the student directly sets
/// a weight) and implicit drift (weight nudges toward 1.0 each time the student engages with
/// goal-tagged content) write to the same <see cref="Weight"/> value — this is the "explicit +
/// implicit blend" decided in the architecture discussion, not two separate scores to merge. A goal
/// is never hard-deleted when its weight drops; it can resurface without the student re-declaring
/// it. Additive only — nothing consumes this yet outside the student's own "My Goals" view. See
/// docs/architecture/adaptive-curriculum-skill-graph.md.
/// </summary>
public sealed class StudentGoalWeight : BaseEntity
{
    public Guid StudentId { get; private set; }

    /// <summary>Must be a value from <see cref="CurriculumContextTagConstants.GoalTags"/>.</summary>
    public string GoalTag { get; private set; }

    /// <summary>0 (not relevant) to 1 (highly relevant). Not a probability simplex — each tag is an
    /// independent relevance score, not required to sum to 1 across a student's goals.</summary>
    public double Weight { get; private set; }

    public StudentGoalWeightSource Source { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private StudentGoalWeight() { }

    public StudentGoalWeight(Guid studentId, string goalTag, double weight, StudentGoalWeightSource source)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId must not be empty.", nameof(studentId));
        if (!CurriculumContextTagConstants.IsGoalTag(goalTag))
            throw new ArgumentException($"'{goalTag}' is not a recognized goal tag.", nameof(goalTag));
        if (weight is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be between 0 and 1.");

        StudentId = studentId;
        GoalTag = goalTag;
        Weight = weight;
        Source = source;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetExplicitWeight(double weight)
    {
        if (weight is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be between 0 and 1.");

        Weight = weight;
        Source = StudentGoalWeightSource.Explicit;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Bounded exponential-moving-average nudge toward 1.0 — never an open-ended increment,
    /// never exceeds 1. <paramref name="alpha"/> is the step size (0-1); a larger recent-engagement
    /// signal moves the weight further, but always by a shrinking fraction of the remaining gap to
    /// 1.0, so no single activity can dominate the score.</summary>
    public void ApplyImplicitEngagement(double alpha)
    {
        if (alpha is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be between 0 (exclusive) and 1.");

        Weight += alpha * (1 - Weight);
        Source = StudentGoalWeightSource.Implicit;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
