using LinguaCoach.Application.TodayPlanModules;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H6 (renamed I4 Pass 3) — admin-only, read-only diagnostics for the Today Plan module
/// pipeline: which approved Modules would be (or were) selected for a student's Today, and why a
/// fallback was used when one was. The preview endpoint never mutates anything — it calls the
/// selector directly, bypassing the assignment recorder, so previewing has no side effects.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminTodayPlanModuleController : ControllerBase
{
    private readonly ITodayPlanModuleSelectionService _selector;
    private readonly LinguaCoachDbContext _db;

    public AdminTodayPlanModuleController(ITodayPlanModuleSelectionService selector, LinguaCoachDbContext db)
    {
        _selector = selector;
        _db = db;
    }

    /// <summary>
    /// Previews which approved Modules the deterministic selector would choose for a student
    /// today (or a given date), and the fallback reason if none would be chosen. Read-only —
    /// does not record a <c>StudentTodayPlanModuleAssignment</c> row.
    /// </summary>
    [HttpGet("api/admin/today-plan/modules/preview")]
    public async Task<IActionResult> PreviewSelection(
        [FromQuery] Guid studentId, [FromQuery] int? maxModules, [FromQuery] DateTime? targetDate, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == studentId, ct);
        if (profile is null)
            return NotFound(new { error = "Student not found." });

        var result = await _selector.SelectAsync(
            new TodayPlanModuleSelectionRequest(
                StudentId: studentId,
                CefrLevel: profile.CefrLevel,
                LearningPlanId: null,
                TargetDate: (targetDate ?? DateTime.UtcNow).Date,
                MaxModules: maxModules ?? 1),
            ct);

        return Ok(result);
    }

    /// <summary>
    /// Returns recent Today Plan module assignment bookkeeping rows for a student
    /// (most recent first). A date with no row for it means the fallback path was used that
    /// day — see <c>TodayPlanModuleAssignmentRecorder</c>'s doc comment. Read-only.
    /// </summary>
    [HttpGet("api/admin/today-plan/students/{studentId:guid}/assignments")]
    public async Task<IActionResult> GetAssignmentHistory(Guid studentId, [FromQuery] int? days, CancellationToken ct)
    {
        var lookbackDays = days is > 0 and <= 365 ? days.Value : 30;
        var since = DateTime.UtcNow.Date.AddDays(-lookbackDays);

        var assignments = await _db.StudentTodayPlanModuleAssignments
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.AssignedForDate >= since)
            .OrderByDescending(a => a.AssignedForDate)
            .ToListAsync(ct);

        var moduleIds = assignments
            .Where(a => a.ModuleId.HasValue)
            .Select(a => a.ModuleId!.Value)
            .Distinct()
            .ToList();

        var titlesById = await _db.Modules
            .AsNoTracking()
            .Where(m => moduleIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Title })
            .ToDictionaryAsync(m => m.Id, m => m.Title, ct);

        var items = assignments.Select(a => new
        {
            a.Id,
            a.AssignedForDate,
            ModuleId = a.ModuleId,
            ModuleTitle = a.ModuleId.HasValue ? titlesById.GetValueOrDefault(a.ModuleId.Value) : null,
            Status = a.Status.ToString(),
            a.SelectionReason,
            a.FallbackReason,
            a.EstimatedMinutes,
            a.ConsumedAt,
            a.CreatedAt
        });

        return Ok(new { studentId, lookbackDays, assignments = items });
    }

    /// <summary>
    /// Phase rehaul (2026-07-17) — fleet-wide, read-only delivery health for Today's bank-first
    /// module pipeline: what fraction of eligible (CEFR-placed) students got a Module selected
    /// today vs. fell back, broken down by CEFR level, a trend over the lookback window, the
    /// top fallback reasons, and a bank-coverage check flagging CEFR levels with eligible
    /// students but zero approved Modules. Replaces the deleted legacy generation-buffer/
    /// readiness-pool health surfaces (see docs/reviews/2026-07-10-phase-i2b-*.md, -i2c-*.md).
    /// </summary>
    [HttpGet("api/admin/today-plan/delivery-health")]
    public async Task<IActionResult> GetDeliveryHealth([FromQuery] int? days, CancellationToken ct)
    {
        var lookbackDays = days is > 0 and <= 90 ? days.Value : 7;
        var today = DateTime.UtcNow.Date;
        var since = today.AddDays(-(lookbackDays - 1));

        // Eligible = has a CEFR level, i.e. can actually be matched by the selector.
        var eligibleByCefr = await _db.StudentProfiles.AsNoTracking()
            .Where(p => p.CefrLevel != null)
            .GroupBy(p => p.CefrLevel!)
            .Select(g => new { CefrLevel = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var totalEligible = eligibleByCefr.Sum(g => g.Count);

        var windowAssignments = await _db.StudentTodayPlanModuleAssignments.AsNoTracking()
            .Where(a => a.AssignedForDate >= since && a.AssignedForDate <= today)
            .Select(a => new { a.StudentId, a.AssignedForDate, a.Status, a.FallbackReason })
            .ToListAsync(ct);

        var todaysAssignments = windowAssignments.Where(a => a.AssignedForDate == today).ToList();
        var todaySelected = todaysAssignments.Count(a => a.Status != TodayPlanModuleAssignmentStatus.FallbackOnly);
        var todayFallback = todaysAssignments.Count(a => a.Status == TodayPlanModuleAssignmentStatus.FallbackOnly);
        var todayResult = new TodayPlanDeliveryHealthToday(
            EligibleStudents: totalEligible,
            SelectedCount: todaySelected,
            FallbackOnlyCount: todayFallback,
            NoAssignmentCount: Math.Max(0, totalEligible - todaysAssignments.Count));

        var studentCefr = await _db.StudentProfiles.AsNoTracking()
            .Where(p => p.CefrLevel != null)
            .Select(p => new { p.Id, p.CefrLevel })
            .ToDictionaryAsync(p => p.Id, p => p.CefrLevel!, ct);

        var byCefrLevel = eligibleByCefr
            .Select(g => new TodayPlanDeliveryHealthCefrBucket(
                CefrLevel: g.CefrLevel,
                EligibleStudents: g.Count,
                SelectedCount: todaysAssignments.Count(a =>
                    studentCefr.TryGetValue(a.StudentId, out var level) && level == g.CefrLevel
                    && a.Status != TodayPlanModuleAssignmentStatus.FallbackOnly),
                FallbackOnlyCount: todaysAssignments.Count(a =>
                    studentCefr.TryGetValue(a.StudentId, out var level) && level == g.CefrLevel
                    && a.Status == TodayPlanModuleAssignmentStatus.FallbackOnly)))
            .OrderBy(b => Array.IndexOf(CefrLevelConstants.All.ToArray(), b.CefrLevel))
            .ToList();

        var trend = Enumerable.Range(0, lookbackDays)
            .Select(offset => since.AddDays(offset))
            .Select(date => new TodayPlanDeliveryHealthTrendBucket(
                Date: date,
                SelectedCount: windowAssignments.Count(a => a.AssignedForDate == date && a.Status != TodayPlanModuleAssignmentStatus.FallbackOnly),
                FallbackOnlyCount: windowAssignments.Count(a => a.AssignedForDate == date && a.Status == TodayPlanModuleAssignmentStatus.FallbackOnly)))
            .ToList();

        var topFallbackReasons = windowAssignments
            .Where(a => a.Status == TodayPlanModuleAssignmentStatus.FallbackOnly && !string.IsNullOrWhiteSpace(a.FallbackReason))
            .GroupBy(a => a.FallbackReason!)
            .Select(g => new TodayPlanDeliveryHealthFallbackReason(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .Take(5)
            .ToList();

        var approvedModulesByCefr = await _db.Modules.AsNoTracking()
            .Where(m => m.ReviewStatus == AdminReviewStatus.Approved && !m.IsArchived && m.CefrLevel != null)
            .GroupBy(m => m.CefrLevel!)
            .Select(g => new { CefrLevel = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CefrLevel, g => g.Count, ct);

        var bankCoverage = eligibleByCefr
            .Select(g => new TodayPlanDeliveryHealthBankCoverage(
                CefrLevel: g.CefrLevel,
                EligibleStudents: g.Count,
                ApprovedModuleCount: approvedModulesByCefr.GetValueOrDefault(g.CefrLevel, 0)))
            .OrderBy(b => Array.IndexOf(CefrLevelConstants.All.ToArray(), b.CefrLevel))
            .ToList();

        return Ok(new TodayPlanDeliveryHealthResult(todayResult, byCefrLevel, trend, topFallbackReasons, bankCoverage));
    }
}
