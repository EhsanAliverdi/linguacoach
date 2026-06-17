using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Application.UsageGovernance;

public interface IUsageQuotaService
{
    /// <summary>
    /// Checks if the student is allowed to use a feature before the call is made.
    /// </summary>
    Task<QuotaDecision> CheckAsync(
        Guid studentProfileId,
        string featureKey,
        long estimatedUnits = 1,
        decimal? estimatedCost = null,
        CancellationToken ct = default);

    /// <summary>
    /// Records actual usage after a feature/provider call completes.
    /// </summary>
    Task RecordAsync(UsageEvent usageEvent, CancellationToken ct = default);

    /// <summary>
    /// Returns aggregated usage for a student over a date range.
    /// </summary>
    Task<UsageSummary> GetUsageSummaryAsync(
        Guid studentProfileId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the effective usage policy for a student (student override or global default).
    /// </summary>
    Task<Domain.Entities.UsagePolicy?> GetEffectivePolicyAsync(
        Guid studentProfileId,
        CancellationToken ct = default);
}
