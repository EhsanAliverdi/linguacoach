using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Read-only admin endpoint for inspecting a student's activity readiness pool.
/// No write endpoints — pool state is managed by the pool service and generation jobs.
/// </summary>
[ApiController]
[Route("api/admin/students")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminReadinessPoolController : ControllerBase
{
    private readonly IStudentActivityReadinessPoolService _poolService;
    private readonly IReadinessPoolReplenishmentService _replenishment;

    public AdminReadinessPoolController(
        IStudentActivityReadinessPoolService poolService,
        IReadinessPoolReplenishmentService replenishment)
    {
        _poolService = poolService;
        _replenishment = replenishment;
    }

    /// <summary>Returns the readiness pool summary and items for a student.</summary>
    [HttpGet("{studentId:guid}/readiness-pool")]
    public async Task<IActionResult> GetReadinessPool(Guid studentId, CancellationToken ct)
    {
        var summary = await _poolService.GetPoolSummaryAsync(studentId, ct);
        return Ok(summary);
    }

    /// <summary>
    /// Returns pool health for a student across TodayLesson and PracticeGym pools.
    /// Includes ready count, target count, shortfall, and queued/generating in-flight counts.
    /// Read-only. No side effects.
    /// </summary>
    [HttpGet("{studentId:guid}/readiness-pool/health")]
    public async Task<IActionResult> GetPoolHealth(Guid studentId, CancellationToken ct)
    {
        var todayHealth = await _replenishment.GetHealthAsync(studentId, ReadinessPoolSource.TodayLesson, ct);
        var gymHealth = await _replenishment.GetHealthAsync(studentId, ReadinessPoolSource.PracticeGym, ct);

        return Ok(new
        {
            studentId,
            todayLesson = new
            {
                source = todayHealth.Source.ToString(),
                targetCount = todayHealth.TargetCount,
                readyCount = todayHealth.ReadyCount,
                reservedCount = todayHealth.ReservedCount,
                queuedOrGeneratingCount = todayHealth.QueuedOrGeneratingCount,
                failedCount = todayHealth.FailedCount,
                staleCount = todayHealth.StaleCount,
                expiredCount = todayHealth.ExpiredCount,
                skippedCount = todayHealth.SkippedCount,
                reviewOnlyCount = todayHealth.ReviewOnlyCount,
                shortfallCount = todayHealth.ShortfallCount,
                needsReplenishment = todayHealth.NeedsReplenishment
            },
            practiceGym = new
            {
                source = gymHealth.Source.ToString(),
                targetCount = gymHealth.TargetCount,
                readyCount = gymHealth.ReadyCount,
                reservedCount = gymHealth.ReservedCount,
                queuedOrGeneratingCount = gymHealth.QueuedOrGeneratingCount,
                failedCount = gymHealth.FailedCount,
                staleCount = gymHealth.StaleCount,
                expiredCount = gymHealth.ExpiredCount,
                skippedCount = gymHealth.SkippedCount,
                reviewOnlyCount = gymHealth.ReviewOnlyCount,
                shortfallCount = gymHealth.ShortfallCount,
                needsReplenishment = gymHealth.NeedsReplenishment
            }
        });
    }
}
