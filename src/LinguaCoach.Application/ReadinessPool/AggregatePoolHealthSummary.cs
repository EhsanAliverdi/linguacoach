namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// System-wide aggregate readiness pool health across all students.
/// Aggregated in the database — no per-student iteration.
/// </summary>
public sealed class AggregatePoolHealthSummary
{
    public int TotalStudentsWithItems { get; init; }
    public int TotalQueued { get; init; }
    public int TotalGenerating { get; init; }
    public int TotalReady { get; init; }
    public int TotalReserved { get; init; }
    public int TotalConsumed { get; init; }
    public int TotalExpired { get; init; }
    public int TotalFailed { get; init; }
    public int TotalStale { get; init; }
    public int TotalReviewOnly { get; init; }
    public int TotalSkipped { get; init; }

    /// <summary>Students who have zero Ready items across both sources.</summary>
    public int StudentsWithNoReadyItems { get; init; }

    /// <summary>Oldest Ready item creation timestamp. Null when pool is empty.</summary>
    public DateTime? OldestReadyItemCreatedAt { get; init; }

    /// <summary>Most recently created item timestamp across all statuses. Null when pool is empty.</summary>
    public DateTime? NewestItemCreatedAt { get; init; }

    /// <summary>Students who have at least one Failed item.</summary>
    public int StudentsWithFailedItems { get; init; }

    /// <summary>Students who have at least one Stale item.</summary>
    public int StudentsWithStaleItems { get; init; }

    /// <summary>
    /// Students whose Ready count is below the configured minimum threshold.
    /// Populated by the controller using ReadinessPoolReplenishmentOptions.MinimumReadyThreshold.
    /// </summary>
    public int StudentsBelowMinimumThreshold { get; init; }

    /// <summary>
    /// Average Ready items per student with items in the pool.
    /// Returns 0 when no students have items.
    /// </summary>
    public double AverageReadyPerStudent { get; init; }

    public DateTime GeneratedAt { get; init; }
}
