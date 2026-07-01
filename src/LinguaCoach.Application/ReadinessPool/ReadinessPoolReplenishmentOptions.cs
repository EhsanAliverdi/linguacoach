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

    /// <summary>
    /// When true, review scaffold generation logic runs but does not write to the database.
    /// Useful for production dry-runs before fully enabling review generation.
    /// Only meaningful when EnableReviewScaffoldGeneration is also true.
    /// Defaults to true: flipping EnableReviewScaffoldGeneration on always requires an
    /// explicit second step (setting this to false) before generation goes live.
    /// </summary>
    public bool DryRunOnly { get; set; } = true;

    /// <summary>
    /// When true, review/scaffold items are stamped RequiresAdminReview=true at creation and
    /// stay hidden from Practice Gym suggestions until an admin sets this to false after
    /// inspecting the pending-review admin list. Global flag, not a per-item approval workflow.
    /// </summary>
    public bool RequireAdminReview { get; set; } = true;

    /// <summary>Maximum review/scaffold items created for a single student in a calendar day (UTC).</summary>
    public int MaxScaffoldItemsPerStudentPerDay { get; set; } = 3;

    /// <summary>
    /// Readiness pool sources (ReadinessPoolSource names) allowed to receive review/scaffold
    /// generation. Conservative default excludes TodayLesson.
    /// </summary>
    public string[] ScaffoldAllowedSources { get; set; } = ["PracticeGym"];

    /// <summary>
    /// Explicit override required (in addition to ScaffoldAllowedSources containing
    /// "TodayLesson") before review/scaffold items may be generated for the Today lesson pool.
    /// </summary>
    public bool AllowTodayLessonInsertion { get; set; } = false;

    /// <summary>
    /// Minimum ReviewNeedConfidence band required before a weak-event signal is allowed to
    /// trigger review/scaffold generation. Stored as string for appsettings readability.
    /// </summary>
    public string MinimumConfidenceForReviewNeed { get; set; } = "Medium";

    /// <summary>
    /// Minimum ready items below which a warning is surfaced in admin health metrics.
    /// Does not block serving — used only for observability.
    /// Must be less than or equal to TodayLessonPoolTargetCount / PracticeGymPoolTargetCount.
    /// </summary>
    public int MinimumReadyThreshold { get; set; } = 3;

    /// <summary>
    /// Maximum ready+queued+generating items per student per source.
    /// Replenishment will not queue new items beyond this cap even if shortfall math says otherwise.
    /// Prevents runaway over-fill when a student has many reserved-but-not-consumed items.
    /// </summary>
    public int MaxBufferCount { get; set; } = 20;
}
