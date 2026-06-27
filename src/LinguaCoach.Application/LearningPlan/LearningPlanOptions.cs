namespace LinguaCoach.Application.LearningPlan;

/// <summary>
/// Configuration options for learning plan generation.
/// Bound from the "LearningPlan" appsettings section.
/// </summary>
public sealed class LearningPlanOptions
{
    public const string SectionName = "LearningPlan";

    /// <summary>Target number of planned lessons maintained in the queue. Default 10.</summary>
    public int PlannedLessonCount { get; set; } = 10;

    /// <summary>
    /// Maximum number of upcoming objectives surfaced in the plan summary. Default 5.
    /// </summary>
    public int MaxUpcomingObjectives { get; set; } = 5;

    /// <summary>
    /// Maximum number of objectives returned for Practice Gym alignment. Default 5.
    /// </summary>
    public int MaxPracticeGymObjectives { get; set; } = 5;

    /// <summary>
    /// Minimum mastery percentage (0–100) to consider an objective completed. Default 70.
    /// </summary>
    public int MasteryCompletionThreshold { get; set; } = 70;
}
