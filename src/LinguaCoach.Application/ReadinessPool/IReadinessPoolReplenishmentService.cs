using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// Orchestrates readiness pool health maintenance for all active students.
/// Responsibilities:
///   - Calculate pool health per student/source.
///   - Queue new items to fill shortfalls (up to MaxItemsGeneratedPerRun).
///   - Expire stale ready/reserved items past configured age.
///   - Recover orphaned generating items (timeout → failed/stale).
///   - Schedule retry for failed items within attempt limits.
///   - Prevent duplicate queued/generating/ready items for same objective/pattern/CEFR.
/// </summary>
public interface IReadinessPoolReplenishmentService
{
    /// <summary>
    /// Runs one full replenishment cycle.
    /// Returns a summary of what was done for observability.
    /// </summary>
    Task<ReplenishmentRunSummary> RunAsync(CancellationToken ct = default);

    /// <summary>
    /// Calculates pool health for a single student + source.
    /// Does not modify any data.
    /// </summary>
    Task<PoolHealthSummary> GetHealthAsync(
        Guid studentId,
        ReadinessPoolSource source,
        CancellationToken ct = default);
}

/// <summary>Summary of one replenishment run for logging and admin inspection.</summary>
public sealed class ReplenishmentRunSummary
{
    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public int StudentsProcessed { get; init; }
    public int ItemsQueued { get; init; }
    public int ItemsExpired { get; init; }
    public int ItemsRecoveredFromGenerating { get; init; }
    public int ItemsRetryQueued { get; init; }
    public int ItemsMarkedStale { get; init; }
    public int SkippedDuplicates { get; init; }
    public bool HitMaxItemsLimit { get; init; }

    /// <summary>Items skipped because the student's pool already reached MaxBufferCount.</summary>
    public int SkippedAtMaxBuffer { get; init; }

    /// <summary>
    /// Review/scaffold slots where generation fell back to Normal routing because the
    /// student already reached MaxScaffoldItemsPerStudentPerDay for today.
    /// </summary>
    public int SkippedDailyCapReached { get; init; }

    /// <summary>Elapsed duration in milliseconds.</summary>
    public long ElapsedMs => (long)(CompletedAt - StartedAt).TotalMilliseconds;

    /// <summary>
    /// Fraction of slots that resulted in a queued item vs skipped (duplicate or max-buffer).
    /// Range 0.0–1.0. Returns 1.0 when nothing was attempted (avoid division by zero).
    /// </summary>
    public double GenerationSuccessRate
    {
        get
        {
            var total = ItemsQueued + SkippedDuplicates + SkippedAtMaxBuffer;
            return total == 0 ? 1.0 : (double)ItemsQueued / total;
        }
    }
}
