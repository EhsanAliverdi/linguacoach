namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// Configuration for the background readiness pool replenishment engine.
/// Values may be overridden via appsettings.json under "ReadinessPool".
/// TODO: move to DB-backed admin config in a future phase (10O+).
/// </summary>
public sealed class ReadinessPoolReplenishmentOptions
{
    public const string SectionName = "ReadinessPool";

    /// <summary>Target number of ready Today lesson items per active student.</summary>
    public int TodayLessonPoolTargetCount { get; set; } = 10;

    /// <summary>Target number of ready Practice Gym items per active student.</summary>
    public int PracticeGymPoolTargetCount { get; set; } = 10;

    /// <summary>Maximum generation attempts per item before it is abandoned.</summary>
    public int MaxGenerationAttempts { get; set; } = 3;

    /// <summary>Days after creation before a ready item expires.</summary>
    public int ReadyItemExpiryDays { get; set; } = 14;

    /// <summary>Hours after reservation before a reserved item expires (stuck reservation).</summary>
    public int ReservedItemExpiryHours { get; set; } = 2;

    /// <summary>Minutes after a generating item was last updated before it is considered orphaned.</summary>
    public int GeneratingTimeoutMinutes { get; set; } = 30;

    /// <summary>Minutes a failed item must wait before being retried.</summary>
    public int FailedRetryDelayMinutes { get; set; } = 60;

    /// <summary>Maximum new items created (queued) in a single replenishment run across all students.</summary>
    public int MaxItemsGeneratedPerRun { get; set; } = 50;

    /// <summary>
    /// When true, replenishment may generate lower-level review/scaffold items when ledger signals
    /// show clear weakness. Conservative default: false. Enable only after ledger/weakness
    /// signals are validated in production.
    /// TODO: enable after mastery/weakness engine is proven (Phase 10O+).
    /// </summary>
    public bool EnableReviewScaffoldGeneration { get; set; } = false;
}
