using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Read-only admin endpoint for inspecting a student's activity readiness pool.
/// No write endpoints — pool state is managed by the pool service and generation jobs.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminReadinessPoolController : ControllerBase
{
    private readonly IStudentActivityReadinessPoolService _poolService;
    private readonly IReadinessPoolReplenishmentService _replenishment;
    private readonly LinguaCoachDbContext _db;

    public AdminReadinessPoolController(
        IStudentActivityReadinessPoolService poolService,
        IReadinessPoolReplenishmentService replenishment,
        LinguaCoachDbContext db)
    {
        _poolService = poolService;
        _replenishment = replenishment;
        _db = db;
    }

    /// <summary>Returns the readiness pool summary and items for a student.</summary>
    [HttpGet("api/admin/students/{studentId:guid}/readiness-pool")]
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
    [HttpGet("api/admin/students/{studentId:guid}/readiness-pool/health")]
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

    /// <summary>
    /// Returns system-wide aggregate readiness pool health across all students.
    /// All aggregation is done in the database — no per-student iteration.
    /// Read-only. No side effects.
    /// </summary>
    [HttpGet("api/admin/readiness-pool/health")]
    public async Task<IActionResult> GetAggregatePoolHealth(CancellationToken ct)
    {
        var items = _db.StudentActivityReadinessItems.AsNoTracking();

        // All counts in a single pass using GroupBy on status (stored as string via HasConversion<string>)
        var statusCounts = await items
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Get(ReadinessPoolStatus s) =>
            statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        var totalQueued     = Get(ReadinessPoolStatus.Queued);
        var totalGenerating = Get(ReadinessPoolStatus.Generating);
        var totalReady      = Get(ReadinessPoolStatus.Ready);
        var totalReserved   = Get(ReadinessPoolStatus.Reserved);
        var totalConsumed   = Get(ReadinessPoolStatus.Consumed);
        var totalExpired    = Get(ReadinessPoolStatus.Expired);
        var totalFailed     = Get(ReadinessPoolStatus.Failed);
        var totalStale      = Get(ReadinessPoolStatus.Stale);
        var totalReviewOnly = Get(ReadinessPoolStatus.ReviewOnly);
        var totalSkipped    = Get(ReadinessPoolStatus.Skipped);

        var totalStudentsWithItems = await items
            .Select(i => i.StudentId)
            .Distinct()
            .CountAsync(ct);

        var studentsWithNoReadyItems = totalStudentsWithItems > 0
            ? totalStudentsWithItems - await items
                .Where(i => i.Status == ReadinessPoolStatus.Ready)
                .Select(i => i.StudentId)
                .Distinct()
                .CountAsync(ct)
            : 0;

        var studentsWithFailedItems = await items
            .Where(i => i.Status == ReadinessPoolStatus.Failed)
            .Select(i => i.StudentId)
            .Distinct()
            .CountAsync(ct);

        var studentsWithStaleItems = await items
            .Where(i => i.Status == ReadinessPoolStatus.Stale)
            .Select(i => i.StudentId)
            .Distinct()
            .CountAsync(ct);

        var oldestReadyCreatedAt = await items
            .Where(i => i.Status == ReadinessPoolStatus.Ready)
            .OrderBy(i => i.CreatedAt)
            .Select(i => (DateTime?)i.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var newestItemCreatedAt = await items
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => (DateTime?)i.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var summary = new AggregatePoolHealthSummary
        {
            TotalStudentsWithItems   = totalStudentsWithItems,
            TotalQueued              = totalQueued,
            TotalGenerating          = totalGenerating,
            TotalReady               = totalReady,
            TotalReserved            = totalReserved,
            TotalConsumed            = totalConsumed,
            TotalExpired             = totalExpired,
            TotalFailed              = totalFailed,
            TotalStale               = totalStale,
            TotalReviewOnly          = totalReviewOnly,
            TotalSkipped             = totalSkipped,
            StudentsWithNoReadyItems = studentsWithNoReadyItems,
            OldestReadyItemCreatedAt = oldestReadyCreatedAt,
            NewestItemCreatedAt      = newestItemCreatedAt,
            StudentsWithFailedItems  = studentsWithFailedItems,
            StudentsWithStaleItems   = studentsWithStaleItems,
            GeneratedAt              = DateTime.UtcNow,
        };

        return Ok(summary);
    }
}
