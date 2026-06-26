using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// Lightweight pool health snapshot for one student + source combination.
/// Used by replenishment logic and admin diagnostics.
/// </summary>
public sealed class PoolHealthSummary
{
    public Guid StudentId { get; init; }
    public ReadinessPoolSource Source { get; init; }
    public int ReadyCount { get; init; }
    public int ReservedCount { get; init; }
    public int QueuedOrGeneratingCount { get; init; }
    public int FailedCount { get; init; }
    public int StaleCount { get; init; }
    public int ExpiredCount { get; init; }
    public int SkippedCount { get; init; }
    public int ReviewOnlyCount { get; init; }
    public int TargetCount { get; init; }
    public int ShortfallCount => Math.Max(0, TargetCount - ReadyCount - QueuedOrGeneratingCount);
    public bool NeedsReplenishment => ShortfallCount > 0;
}
