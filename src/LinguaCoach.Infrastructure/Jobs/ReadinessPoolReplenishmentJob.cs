using LinguaCoach.Application.ReadinessPool;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Scheduled Quartz job that runs the readiness pool replenishment engine.
/// Runs periodically (default: every 20 minutes, configured in QuartzConfiguration).
///
/// Responsibilities delegated to IReadinessPoolReplenishmentService:
///   - Expire stale ready/reserved items.
///   - Recover orphaned generating items.
///   - Retry failed items within attempt limits.
///   - Fill pool shortfalls for all active students.
///   - Prevent duplicate generation.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ReadinessPoolReplenishmentJob : IJob
{
    public const string JobName = "readiness-pool-replenishment";

    private readonly IReadinessPoolReplenishmentService _service;
    private readonly ILogger<ReadinessPoolReplenishmentJob> _logger;

    public ReadinessPoolReplenishmentJob(
        IReadinessPoolReplenishmentService service,
        ILogger<ReadinessPoolReplenishmentJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("ReadinessPoolReplenishmentJob starting.");

        try
        {
            var summary = await _service.RunAsync(context.CancellationToken);

            _logger.LogInformation(
                "ReadinessPoolReplenishmentJob complete in {Ms}ms. students={S} queued={Q} expired={E} recovered={R} retried={Rt} limitHit={L}",
                (summary.CompletedAt - summary.StartedAt).TotalMilliseconds,
                summary.StudentsProcessed,
                summary.ItemsQueued,
                summary.ItemsExpired,
                summary.ItemsRecoveredFromGenerating,
                summary.ItemsRetryQueued,
                summary.HitMaxItemsLimit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadinessPoolReplenishmentJob failed.");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
