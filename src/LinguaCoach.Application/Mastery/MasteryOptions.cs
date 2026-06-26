namespace LinguaCoach.Application.Mastery;

/// <summary>
/// Configuration thresholds for the mastery re-evaluation engine.
/// Bound from appsettings.json section "Mastery".
/// </summary>
public sealed class MasteryOptions
{
    public const string SectionName = "Mastery";

    /// <summary>Minimum average score (0-100) required to classify as Mastered.</summary>
    public int MasteryScoreThreshold { get; set; } = 80;

    /// <summary>Minimum number of learning events required to reach Mastered.</summary>
    public int EvidenceCountThreshold { get; set; } = 5;

    /// <summary>Number of consecutive successes required for Mastered classification.</summary>
    public int ConsecutiveSuccessThreshold { get; set; } = 3;

    /// <summary>Window (days) used to determine recency for review scheduling.</summary>
    public int ReviewRecencyWindowDays { get; set; } = 30;

    /// <summary>Minimum hours between scheduled evaluation sweeps for a student.</summary>
    public int StaleEvaluationIntervalHours { get; set; } = 24;

    /// <summary>Days after which a never-consumed Ready item is considered expired.</summary>
    public int StaleDaysThreshold { get; set; } = 90;
}
