using System.Security.Claims;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin endpoint for inspecting a student's activity readiness pool. Mostly read-only —
/// pool state is managed by the pool service and generation jobs — except for the
/// Phase 19B review scaffold approve/reject/reopen actions below, which mutate only
/// AdminReviewStatus (never CEFR, objective completion, or the Learning Plan).
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminReadinessPoolController : ControllerBase
{
    private readonly IStudentActivityReadinessPoolService _poolService;
    private readonly IReadinessPoolReplenishmentService _replenishment;
    private readonly IStudentMasteryEvaluationService _mastery;
    private readonly IStudentLearningLedger _ledger;
    private readonly ILearningPlanService _learningPlan;
    private readonly ReadinessPoolReplenishmentOptions _replenishmentOpts;
    private readonly LinguaCoachDbContext _db;

    public AdminReadinessPoolController(
        IStudentActivityReadinessPoolService poolService,
        IReadinessPoolReplenishmentService replenishment,
        IStudentMasteryEvaluationService mastery,
        IStudentLearningLedger ledger,
        ILearningPlanService learningPlan,
        IOptions<ReadinessPoolReplenishmentOptions> replenishmentOpts,
        LinguaCoachDbContext db)
    {
        _poolService = poolService;
        _replenishment = replenishment;
        _mastery = mastery;
        _ledger = ledger;
        _learningPlan = learningPlan;
        _replenishmentOpts = replenishmentOpts.Value;
        _db = db;
    }

    private Guid AdminUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("No admin user id in token."));

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

        var studentsWithReadyItems = totalStudentsWithItems > 0
            ? await items
                .Where(i => i.Status == ReadinessPoolStatus.Ready)
                .Select(i => i.StudentId)
                .Distinct()
                .CountAsync(ct)
            : 0;

        var studentsWithNoReadyItems = totalStudentsWithItems - studentsWithReadyItems;

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

        // Students below minimum threshold: fewer Ready items than configured minimum.
        var minThreshold = _replenishmentOpts.MinimumReadyThreshold;
        var studentsBelowMinimum = totalStudentsWithItems > 0
            ? await items
                .Where(i => i.Status == ReadinessPoolStatus.Ready)
                .GroupBy(i => i.StudentId)
                .Select(g => new { StudentId = g.Key, Count = g.Count() })
                .CountAsync(g => g.Count < minThreshold, ct)
            : 0;

        // Students with zero ready items also count as below threshold.
        studentsBelowMinimum += studentsWithNoReadyItems;

        var averageReadyPerStudent = totalStudentsWithItems > 0
            ? (double)totalReady / totalStudentsWithItems
            : 0.0;

        var summary = new AggregatePoolHealthSummary
        {
            TotalStudentsWithItems        = totalStudentsWithItems,
            TotalQueued                   = totalQueued,
            TotalGenerating               = totalGenerating,
            TotalReady                    = totalReady,
            TotalReserved                 = totalReserved,
            TotalConsumed                 = totalConsumed,
            TotalExpired                  = totalExpired,
            TotalFailed                   = totalFailed,
            TotalStale                    = totalStale,
            TotalReviewOnly               = totalReviewOnly,
            TotalSkipped                  = totalSkipped,
            StudentsWithNoReadyItems      = studentsWithNoReadyItems,
            OldestReadyItemCreatedAt      = oldestReadyCreatedAt,
            NewestItemCreatedAt           = newestItemCreatedAt,
            StudentsWithFailedItems       = studentsWithFailedItems,
            StudentsWithStaleItems        = studentsWithStaleItems,
            StudentsBelowMinimumThreshold = studentsBelowMinimum,
            AverageReadyPerStudent        = Math.Round(averageReadyPerStudent, 1),
            GeneratedAt                   = DateTime.UtcNow,
        };

        return Ok(summary);
    }

    /// <summary>
    /// Returns a mastery validation diagnostic summary across all active students.
    /// Aggregates mastery signal quality — no AI calls, no DB writes, read-only.
    /// </summary>
    [HttpGet("api/admin/mastery/validation-summary")]
    public async Task<IActionResult> GetMasteryValidationSummary(CancellationToken ct)
    {
        var activeStudents = await _db.StudentProfiles
            .Where(p => p.LifecycleStage != StudentLifecycleStage.Archived
                     && p.LifecycleStage >= StudentLifecycleStage.CourseReady
                     && p.OnboardingStatus == OnboardingStatus.Complete)
            .AsNoTracking()
            .Select(p => p.Id)
            .ToListAsync(ct);

        var totalObjectives = 0;
        var countInsufficient = 0;
        var countMastered = 0;
        var countNeedsReview = 0;
        var countNeedsPractice = 0;
        var countAtRisk = 0;
        var masteredExcluded = 0;
        var warnings = new List<string>();

        foreach (var studentId in activeStudents)
        {
            if (ct.IsCancellationRequested) break;

            var report = await _mastery.EvaluateStudentAsync(
                studentId, MasteryEvaluationReason.Manual, ct);

            var allKeys = report.MasteredObjectiveKeys
                .Concat(report.WeakObjectiveKeys)
                .Concat(report.AtRiskObjectiveKeys)
                .ToList();

            totalObjectives += allKeys.Count;
            countMastered    += report.MasteredObjectiveKeys.Count;
            countNeedsReview += report.WeakObjectiveKeys.Count;
            countAtRisk      += report.AtRiskObjectiveKeys.Count;

            masteredExcluded += report.MasteredObjectiveKeys.Count;

            // Insufficient evidence is not in the report — infer from ledger events.
            var events = await _ledger.GetRecentAsync(studentId, limit: 200, ct);
            var objectiveKeysWithEvents = events
                .Where(e => e.PatternKey is not null || e.PrimarySkill is not null)
                .Select(e => e.PatternKey ?? e.PrimarySkill!)
                .Distinct()
                .Count();
            countInsufficient += Math.Max(0, objectiveKeysWithEvents - allKeys.Count);

            // Suspicious: mastered with very few events total
            if (report.MasteredObjectiveKeys.Count > 0 && events.Count < 3)
                warnings.Add($"Student {studentId:N} has {report.MasteredObjectiveKeys.Count} mastered objective(s) but fewer than 3 total events.");

            // Suspicious: all objectives at risk
            if (report.AtRiskObjectiveKeys.Count > 0 && report.MasteredObjectiveKeys.Count == 0
                && report.WeakObjectiveKeys.Count == 0 && allKeys.Count > 0)
                warnings.Add($"Student {studentId:N} has all {report.AtRiskObjectiveKeys.Count} objective(s) at risk.");
        }

        var summary = new MasteryValidationSummary
        {
            TotalStudentsEvaluated = activeStudents.Count,
            TotalObjectivesEvaluated = totalObjectives,
            CountInsufficientEvidence = countInsufficient,
            CountMastered = countMastered,
            CountNeedsReview = countNeedsReview,
            CountNeedsPractice = countNeedsPractice,
            CountAtRisk = countAtRisk,
            MasteredExcludedFromNewLearning = masteredExcluded,
            Warnings = warnings,
            GeneratedAt = DateTime.UtcNow
        };

        return Ok(summary);
    }

    /// <summary>
    /// Dry-run simulation: shows what would happen if EnableReviewScaffoldGeneration were active.
    /// Read-only — never mutates the database.
    /// </summary>
    [HttpGet("api/admin/readiness-pool/review-scaffold/dry-run")]
    public async Task<IActionResult> GetReviewScaffoldDryRun(CancellationToken ct)
    {
        var activeStudents = await _db.StudentProfiles
            .Where(p => p.LifecycleStage != StudentLifecycleStage.Archived
                     && p.LifecycleStage >= StudentLifecycleStage.CourseReady
                     && p.OnboardingStatus == OnboardingStatus.Complete)
            .AsNoTracking()
            .Select(p => p.Id)
            .ToListAsync(ct);

        var studentsEligible = 0;
        var estimatedConversions = 0;
        var blockedDuplicates = 0;
        var blockedInactive = 0;
        var warnings = new List<string>();

        // Load all active curriculum objective keys once.
        var activeCurriculumKeys = await _db.Set<CurriculumObjective>()
            .Where(o => o.IsActive)
            .Select(o => o.Key)
            .ToHashSetAsync(ct);

        foreach (var studentId in activeStudents)
        {
            if (ct.IsCancellationRequested) break;

            // Simulate the weak-event check (same logic as FillShortfallAsync).
            var weakEvents = await _ledger.GetWeakEventsAsync(studentId, limit: 5, ct);
            if (weakEvents.Count == 0) continue;

            studentsEligible++;

            // Find Ready/Reserved items for this student whose objective is mastered.
            var report = await _mastery.EvaluateStudentAsync(
                studentId, MasteryEvaluationReason.Manual, ct);

            if (report.MasteredObjectiveKeys.Count == 0) continue;

            var masteredSet = report.MasteredObjectiveKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var readyItems = await _db.StudentActivityReadinessItems
                .Where(i => i.StudentId == studentId
                         && (i.Status == ReadinessPoolStatus.Ready || i.Status == ReadinessPoolStatus.Reserved)
                         && i.PatternKey != null)
                .Select(i => new { i.PatternKey, i.CurriculumObjectiveKey, i.Status })
                .AsNoTracking()
                .ToListAsync(ct);

            foreach (var item in readyItems)
            {
                var key = item.PatternKey ?? item.CurriculumObjectiveKey;
                if (key is null || !masteredSet.Contains(key)) continue;

                // Check if a ReviewOnly already exists for this student + key.
                var duplicateExists = await _db.StudentActivityReadinessItems
                    .AnyAsync(i => i.StudentId == studentId
                                && i.Status == ReadinessPoolStatus.ReviewOnly
                                && (i.PatternKey == key || i.CurriculumObjectiveKey == key), ct);

                if (duplicateExists)
                {
                    blockedDuplicates++;
                    continue;
                }

                // Check if the objective is active.
                if (item.CurriculumObjectiveKey is not null
                    && !activeCurriculumKeys.Contains(item.CurriculumObjectiveKey))
                {
                    blockedInactive++;
                    continue;
                }

                estimatedConversions++;
            }
        }

        if (!_replenishmentOpts.EnableReviewScaffoldGeneration)
            warnings.Add("EnableReviewScaffoldGeneration is currently false. Enable it in ReadinessPool config to activate review generation.");

        if (studentsEligible == 0 && activeStudents.Count > 0)
            warnings.Add("No active students have weak learning events. Review routing would have no effect.");

        var netNew = Math.Max(0, estimatedConversions - blockedDuplicates);

        var adminReviewRequiredCount = await _db.StudentActivityReadinessItems
            .CountAsync(i => i.RequiresAdminReview
                          && i.AdminReviewStatus == AdminReviewStatus.PendingReview
                          && (i.Status == ReadinessPoolStatus.Ready || i.Status == ReadinessPoolStatus.ReviewOnly), ct);

        var generatedTodayCount = await _db.StudentActivityReadinessItems
            .CountAsync(i => i.CreatedAt >= DateTime.UtcNow.Date
                          && i.RoutingReason != RoutingReason.Normal
                          && i.GeneratedBy != null
                          && i.GeneratedBy.StartsWith("ReadinessPoolReplenishment"), ct);

        var summary = new ReviewScaffoldDryRunSummary
        {
            GenerationEnabled = _replenishmentOpts.EnableReviewScaffoldGeneration,
            DryRunOnly = _replenishmentOpts.DryRunOnly,
            StudentsConsidered = activeStudents.Count,
            StudentsEligibleForReview = studentsEligible,
            EstimatedReviewOnlyConversions = estimatedConversions,
            BlockedDuplicates = blockedDuplicates,
            BlockedInactiveObjectives = blockedInactive,
            EstimatedNetNewReviewItems = netNew,
            RequireAdminReview = _replenishmentOpts.RequireAdminReview,
            MaxScaffoldItemsPerStudentPerDay = _replenishmentOpts.MaxScaffoldItemsPerStudentPerDay,
            ScaffoldAllowedSources = _replenishmentOpts.ScaffoldAllowedSources,
            AllowTodayLessonInsertion = _replenishmentOpts.AllowTodayLessonInsertion,
            MinimumConfidenceForReviewNeed = _replenishmentOpts.MinimumConfidenceForReviewNeed,
            AdminReviewRequiredCount = adminReviewRequiredCount,
            GeneratedTodayCount = generatedTodayCount,
            Warnings = warnings,
            GeneratedAt = DateTime.UtcNow
        };

        return Ok(summary);
    }

    /// <summary>
    /// Returns up to 50 review scaffold items (Pending/Approved/Rejected), most recent first.
    /// Read-only. Includes non-pending items so admins can see decision history and reopen
    /// rejected items from the same table.
    /// </summary>
    [HttpGet("api/admin/readiness-pool/review-scaffold/pending-review")]
    public async Task<IActionResult> GetReviewScaffoldPendingReview(CancellationToken ct)
    {
        var items = await _db.StudentActivityReadinessItems
            .AsNoTracking()
            .Where(i => i.RequiresAdminReview)
            .OrderByDescending(i => i.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(items.Select(ToReviewScaffoldDetailDto));
    }

    /// <summary>
    /// Approves a pending review scaffold item, making it eligible for Practice Gym
    /// suggestions once all other lifecycle gates pass. Idempotent if already approved.
    /// Never updates CEFR, completes objectives, or regenerates the Learning Plan.
    /// </summary>
    [HttpPost("api/admin/readiness-pool/review-scaffold/{itemId:guid}/approve")]
    public async Task<IActionResult> ApproveReviewScaffoldItem(Guid itemId, CancellationToken ct)
    {
        var item = await _db.StudentActivityReadinessItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
            return NotFound(new { error = "Review scaffold item not found." });

        var previousStatus = item.AdminReviewStatus;
        try
        {
            item.ApproveAdminReview(AdminUserId);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }

        if (previousStatus != item.AdminReviewStatus)
        {
            _db.AdminAuditLogs.Add(new AdminAuditLog(
                AdminUserId, "ApproveReviewScaffoldItem", "StudentActivityReadinessItem",
                entityId: item.Id.ToString(),
                targetStudentId: item.StudentId,
                oldValueJson: $"{{\"adminReviewStatus\":\"{previousStatus}\"}}",
                newValueJson: $"{{\"adminReviewStatus\":\"{item.AdminReviewStatus}\"}}"));
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ToReviewScaffoldDetailDto(item));
    }

    /// <summary>
    /// Rejects a review scaffold item, permanently hiding it from students unless
    /// explicitly reopened. Idempotent if already rejected. Requires a reason.
    /// </summary>
    [HttpPost("api/admin/readiness-pool/review-scaffold/{itemId:guid}/reject")]
    public async Task<IActionResult> RejectReviewScaffoldItem(
        Guid itemId, [FromBody] ReviewScaffoldReviewActionRequest request, CancellationToken ct)
    {
        var item = await _db.StudentActivityReadinessItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
            return NotFound(new { error = "Review scaffold item not found." });

        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new { error = "Reason is required to reject a review scaffold item." });

        var previousStatus = item.AdminReviewStatus;
        try
        {
            item.RejectAdminReview(AdminUserId, request.Reason, request.Notes);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }

        if (previousStatus != item.AdminReviewStatus)
        {
            _db.AdminAuditLogs.Add(new AdminAuditLog(
                AdminUserId, "RejectReviewScaffoldItem", "StudentActivityReadinessItem",
                entityId: item.Id.ToString(),
                targetStudentId: item.StudentId,
                oldValueJson: $"{{\"adminReviewStatus\":\"{previousStatus}\"}}",
                newValueJson: $"{{\"adminReviewStatus\":\"{item.AdminReviewStatus}\"}}",
                reason: request.Reason));
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ToReviewScaffoldDetailDto(item));
    }

    /// <summary>
    /// Reopens a rejected review scaffold item back to PendingReview. Idempotent if
    /// already pending. Not allowed once the item has been consumed.
    /// </summary>
    [HttpPost("api/admin/readiness-pool/review-scaffold/{itemId:guid}/reopen")]
    public async Task<IActionResult> ReopenReviewScaffoldItem(
        Guid itemId, [FromBody] ReviewScaffoldReviewActionRequest? request, CancellationToken ct)
    {
        var item = await _db.StudentActivityReadinessItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
            return NotFound(new { error = "Review scaffold item not found." });

        var previousStatus = item.AdminReviewStatus;
        try
        {
            item.ReopenAdminReview(AdminUserId, request?.Notes);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }

        if (previousStatus != item.AdminReviewStatus)
        {
            _db.AdminAuditLogs.Add(new AdminAuditLog(
                AdminUserId, "ReopenReviewScaffoldItem", "StudentActivityReadinessItem",
                entityId: item.Id.ToString(),
                targetStudentId: item.StudentId,
                oldValueJson: $"{{\"adminReviewStatus\":\"{previousStatus}\"}}",
                newValueJson: $"{{\"adminReviewStatus\":\"{item.AdminReviewStatus}\"}}",
                reason: request?.Notes));
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ToReviewScaffoldDetailDto(item));
    }

    /// <summary>
    /// Phase 19C admin monitoring: pilot status plus student-visible/approved/pending/rejected/
    /// consumed/skipped-or-expired counts for review scaffold items. Read-only.
    /// </summary>
    [HttpGet("api/admin/readiness-pool/review-scaffold/pilot-summary")]
    public async Task<IActionResult> GetReviewScaffoldPilotSummary(CancellationToken ct)
    {
        var scaffoldItems = _db.StudentActivityReadinessItems
            .AsNoTracking()
            .Where(i => i.RequiresAdminReview);

        var approvedCount = await scaffoldItems.CountAsync(i => i.AdminReviewStatus == AdminReviewStatus.Approved, ct);
        var pendingCount = await scaffoldItems.CountAsync(i => i.AdminReviewStatus == AdminReviewStatus.PendingReview, ct);
        var rejectedCount = await scaffoldItems.CountAsync(i => i.AdminReviewStatus == AdminReviewStatus.Rejected, ct);
        var consumedCount = await scaffoldItems.CountAsync(i => i.Status == ReadinessPoolStatus.Consumed, ct);
        var skippedOrExpiredCount = await scaffoldItems.CountAsync(i =>
            i.Status == ReadinessPoolStatus.Expired
            || i.Status == ReadinessPoolStatus.Stale
            || i.Status == ReadinessPoolStatus.Failed
            || i.Status == ReadinessPoolStatus.Skipped, ct);

        var studentVisibleQuery = scaffoldItems.Where(i =>
            i.AdminReviewStatus == AdminReviewStatus.Approved
            && (i.Status == ReadinessPoolStatus.Ready || i.Status == ReadinessPoolStatus.ReviewOnly || i.Status == ReadinessPoolStatus.Reserved)
            && i.Source == ReadinessPoolSource.PracticeGym
            && _replenishmentOpts.PracticeGymPilotEnabled);

        var studentVisibleCount = await studentVisibleQuery.CountAsync(ct);

        var recentStudentVisible = await studentVisibleQuery
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(ToPilotItemDto)
            .ToListAsync(ct);

        var recentConsumed = await scaffoldItems
            .Where(i => i.Status == ReadinessPoolStatus.Consumed)
            .OrderByDescending(i => i.UpdatedAt)
            .Take(10)
            .Select(ToPilotItemDto)
            .ToListAsync(ct);

        var summary = new ReviewScaffoldPilotSummaryDto
        {
            PracticeGymPilotEnabled = _replenishmentOpts.PracticeGymPilotEnabled,
            AllowTodayLessonInsertion = _replenishmentOpts.AllowTodayLessonInsertion,
            RequireAdminReview = _replenishmentOpts.RequireAdminReview,
            MaxStudentVisibleScaffoldSuggestions = _replenishmentOpts.MaxStudentVisibleScaffoldSuggestions,
            ApprovedCount = approvedCount,
            StudentVisibleCount = studentVisibleCount,
            PendingReviewCount = pendingCount,
            RejectedCount = rejectedCount,
            ConsumedCount = consumedCount,
            SkippedOrExpiredCount = skippedOrExpiredCount,
            RecentStudentVisibleItems = recentStudentVisible,
            RecentConsumedItems = recentConsumed,
            GeneratedAt = DateTime.UtcNow
        };

        return Ok(summary);
    }

    private static readonly System.Linq.Expressions.Expression<Func<StudentActivityReadinessItem, ReviewScaffoldPilotItemDto>> ToPilotItemDto =
        i => new ReviewScaffoldPilotItemDto
        {
            Id = i.Id,
            StudentId = i.StudentId,
            PrimarySkill = i.PrimarySkill,
            CurriculumObjectiveTitle = i.CurriculumObjectiveTitle,
            Status = i.Status.ToString(),
            CreatedAt = i.CreatedAt
        };

    private static ReviewScaffoldItemDetailDto ToReviewScaffoldDetailDto(StudentActivityReadinessItem i)
    {
        // Note: these flags reflect structural eligibility (lifecycle status + admin approval),
        // same as Phase 19B. They intentionally do NOT factor in PracticeGymPilotEnabled — an
        // item can be "eligible" here while still hidden from students until the Phase 19C
        // pilot flag is on. Use the pilot-summary endpoint for the actual pilot-gated count.
        var lifecycleEligible = i.Status is ReadinessPoolStatus.Ready or ReadinessPoolStatus.ReviewOnly or ReadinessPoolStatus.Reserved;
        var isStudentVisible = lifecycleEligible && i.PassesAdminReviewGate;
        var isPracticeGymEligible = isStudentVisible
            && i.Source == ReadinessPoolSource.PracticeGym
            && i.Status is ReadinessPoolStatus.Ready or ReadinessPoolStatus.ReviewOnly;

        return new ReviewScaffoldItemDetailDto
        {
            Id = i.Id,
            StudentId = i.StudentId,
            ActivityId = i.LearningActivityId,
            Source = i.Source.ToString(),
            Status = i.Status.ToString(),
            TargetCefrLevel = i.TargetCefrLevel,
            PrimarySkill = i.PrimarySkill,
            CurriculumObjectiveKey = i.CurriculumObjectiveKey,
            CurriculumObjectiveTitle = i.CurriculumObjectiveTitle,
            PatternKey = i.PatternKey,
            ActivityType = i.ActivityType,
            RoutingReason = i.RoutingReason.ToString(),
            AdminReviewStatus = i.AdminReviewStatus.ToString(),
            AdminReviewedByUserId = i.AdminReviewedByUserId,
            AdminReviewedAtUtc = i.AdminReviewedAtUtc,
            AdminReviewReason = i.AdminReviewReason,
            AdminReviewNotes = i.AdminReviewNotes,
            IsStudentVisible = isStudentVisible,
            IsPracticeGymEligible = isPracticeGymEligible,
            CreatedAt = i.CreatedAt
        };
    }

    /// <summary>
    /// Returns the learning plan summary for a student (Phase 12D admin visibility).
    /// Read-only. Generates a plan if none exists.
    /// </summary>
    [HttpGet("api/admin/students/{studentId:guid}/learning-plan")]
    public async Task<IActionResult> GetLearningPlan(Guid studentId, CancellationToken ct)
    {
        try
        {
            var summary = await _learningPlan.GetOrCreatePlanAsync(studentId, ct);
            return Ok(summary);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returns the learning plan progress summary for a student.
    /// Read-only. Derived from existing mastery data — no AI calls.
    /// </summary>
    [HttpGet("api/admin/students/{studentId:guid}/learning-plan/progress")]
    public async Task<IActionResult> GetLearningPlanProgress(Guid studentId, CancellationToken ct)
    {
        try
        {
            var progress = await _learningPlan.GetProgressAsync(studentId, ct);
            return Ok(progress);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
