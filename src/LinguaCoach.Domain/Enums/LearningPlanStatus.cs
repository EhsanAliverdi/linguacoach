namespace LinguaCoach.Domain.Enums;

public enum LearningPlanStatus
{
    /// <summary>The current active plan for this student.</summary>
    Active = 0,

    /// <summary>Plan is being regenerated; the student continues using the previous objectives.</summary>
    Regenerating = 1,

    /// <summary>Replaced by a newer plan.</summary>
    Superseded = 2
}
