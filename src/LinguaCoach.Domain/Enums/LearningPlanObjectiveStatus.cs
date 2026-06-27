namespace LinguaCoach.Domain.Enums;

public enum LearningPlanObjectiveStatus
{
    /// <summary>Scheduled for upcoming lessons.</summary>
    Active = 0,

    /// <summary>Student has completed this objective.</summary>
    Completed = 1,

    /// <summary>Student has mastered this objective (confirmed by mastery engine).</summary>
    Mastered = 2,

    /// <summary>Blocked by an unmet prerequisite.</summary>
    Blocked = 3,

    /// <summary>Deferred — not currently scheduled.</summary>
    Deferred = 4,

    /// <summary>Scheduled specifically as a review for a previously completed objective.</summary>
    Review = 5
}
