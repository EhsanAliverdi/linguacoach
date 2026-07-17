using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H7 — admin-only, read-only diagnostics for the Practice Gym module pipeline: which
/// approved Modules would be (or were) suggested for a student's Practice Gym, and why a fallback
/// was used when one was. The preview endpoint never mutates anything — it calls the selector
/// directly, bypassing the assignment recorder, so previewing has no side effects.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminPracticeGymModuleController : ControllerBase
{
    private readonly IPracticeGymModuleSelectionService _selector;
    private readonly LinguaCoachDbContext _db;

    public AdminPracticeGymModuleController(IPracticeGymModuleSelectionService selector, LinguaCoachDbContext db)
    {
        _selector = selector;
        _db = db;
    }

    /// <summary>
    /// Previews which approved Modules the deterministic selector would suggest for a student's
    /// Practice Gym, and the fallback reason if none would be suggested. Read-only — does not
    /// record a <c>StudentPracticeGymModuleAssignment</c> row.
    /// </summary>
    [HttpGet("api/admin/practice-gym/modules/preview")]
    public async Task<IActionResult> PreviewSelection(
        [FromQuery] Guid studentId,
        [FromQuery] int? maxSuggestions,
        [FromQuery] string? skill,
        [FromQuery] string? subskill,
        CancellationToken ct)
    {
        var profile = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == studentId, ct);
        if (profile is null)
            return NotFound(new { error = "Student not found." });

        var result = await _selector.SelectAsync(
            new PracticeGymModuleSelectionRequest(
                StudentId: studentId,
                CefrLevel: profile.CefrLevel,
                RequestedSkill: skill,
                RequestedSubskill: subskill,
                MaxSuggestions: maxSuggestions ?? 4),
            ct);

        return Ok(result);
    }

    /// <summary>
    /// Returns recent Practice Gym module assignment bookkeeping rows for a student (most recent
    /// first). Read-only.
    /// </summary>
    [HttpGet("api/admin/practice-gym/students/{studentId:guid}/assignments")]
    public async Task<IActionResult> GetAssignmentHistory(Guid studentId, [FromQuery] int? days, CancellationToken ct)
    {
        var lookbackDays = days is > 0 and <= 365 ? days.Value : 30;
        var since = DateTimeOffset.UtcNow.AddDays(-lookbackDays);

        var assignments = await _db.StudentPracticeGymModuleAssignments
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.SuggestedAt >= since)
            .OrderByDescending(a => a.SuggestedAt)
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
            a.SuggestedAt,
            ModuleId = a.ModuleId,
            ModuleTitle = a.ModuleId.HasValue ? titlesById.GetValueOrDefault(a.ModuleId.Value) : null,
            Status = a.Status.ToString(),
            a.SelectionReason,
            a.FallbackReason,
            a.SelectedAt,
            a.DismissedAt,
            a.ConsumedAt,
            a.CreatedAt
        });

        return Ok(new { studentId, lookbackDays, assignments = items });
    }

    /// <summary>
    /// Phase rehaul (2026-07-17) — fleet-wide, read-only delivery health for Practice Gym's
    /// bank-first module pipeline. Mirrors <c>AdminTodayPlanModuleController.GetDeliveryHealth</c>;
    /// "suggested" here means at least one Module was suggested that day (any status other than
    /// <see cref="PracticeGymModuleAssignmentStatus.FallbackOnly"/>).
    /// </summary>
    [HttpGet("api/admin/practice-gym/delivery-health")]
    public async Task<IActionResult> GetDeliveryHealth([FromQuery] int? days, CancellationToken ct)
    {
        var lookbackDays = days is > 0 and <= 90 ? days.Value : 7;
        var today = DateTime.UtcNow.Date;
        var since = today.AddDays(-(lookbackDays - 1));
        var sinceOffset = new DateTimeOffset(since, TimeSpan.Zero);

        var eligibleByCefr = await _db.StudentProfiles.AsNoTracking()
            .Where(p => p.CefrLevel != null)
            .GroupBy(p => p.CefrLevel!)
            .Select(g => new { CefrLevel = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var totalEligible = eligibleByCefr.Sum(g => g.Count);

        // SQLite's EF provider cannot translate DateTimeOffset comparisons server-side (same
        // constraint documented in PracticeGymModuleAssignmentRecorder) — fetch then filter client-side.
        var windowAssignments = (await _db.StudentPracticeGymModuleAssignments.AsNoTracking()
            .Select(a => new { a.StudentId, a.SuggestedAt, a.Status, a.FallbackReason })
            .ToListAsync(ct))
            .Where(a => a.SuggestedAt >= sinceOffset)
            .ToList();

        var todaysAssignments = windowAssignments.Where(a => a.SuggestedAt.Date == today).ToList();
        // A student can be suggested-to multiple times per day; count distinct students.
        var todaySelectedStudents = todaysAssignments
            .Where(a => a.Status != PracticeGymModuleAssignmentStatus.FallbackOnly)
            .Select(a => a.StudentId).Distinct().ToHashSet();
        var todayFallbackStudents = todaysAssignments
            .Where(a => a.Status == PracticeGymModuleAssignmentStatus.FallbackOnly)
            .Select(a => a.StudentId).Distinct()
            .Where(id => !todaySelectedStudents.Contains(id)).ToHashSet();
        var todayResult = new PracticeGymDeliveryHealthToday(
            EligibleStudents: totalEligible,
            SelectedCount: todaySelectedStudents.Count,
            FallbackOnlyCount: todayFallbackStudents.Count,
            NoAssignmentCount: Math.Max(0, totalEligible - todaySelectedStudents.Count - todayFallbackStudents.Count));

        var studentCefr = await _db.StudentProfiles.AsNoTracking()
            .Where(p => p.CefrLevel != null)
            .Select(p => new { p.Id, p.CefrLevel })
            .ToDictionaryAsync(p => p.Id, p => p.CefrLevel!, ct);

        var byCefrLevel = eligibleByCefr
            .Select(g => new PracticeGymDeliveryHealthCefrBucket(
                CefrLevel: g.CefrLevel,
                EligibleStudents: g.Count,
                SelectedCount: todaySelectedStudents.Count(id => studentCefr.TryGetValue(id, out var level) && level == g.CefrLevel),
                FallbackOnlyCount: todayFallbackStudents.Count(id => studentCefr.TryGetValue(id, out var level) && level == g.CefrLevel)))
            .OrderBy(b => Array.IndexOf(CefrLevelConstants.All.ToArray(), b.CefrLevel))
            .ToList();

        var trend = Enumerable.Range(0, lookbackDays)
            .Select(offset => since.AddDays(offset))
            .Select(date => new PracticeGymDeliveryHealthTrendBucket(
                Date: date,
                SelectedCount: windowAssignments.Count(a => a.SuggestedAt.Date == date && a.Status != PracticeGymModuleAssignmentStatus.FallbackOnly),
                FallbackOnlyCount: windowAssignments.Count(a => a.SuggestedAt.Date == date && a.Status == PracticeGymModuleAssignmentStatus.FallbackOnly)))
            .ToList();

        var topFallbackReasons = windowAssignments
            .Where(a => a.Status == PracticeGymModuleAssignmentStatus.FallbackOnly && !string.IsNullOrWhiteSpace(a.FallbackReason))
            .GroupBy(a => a.FallbackReason!)
            .Select(g => new PracticeGymDeliveryHealthFallbackReason(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .Take(5)
            .ToList();

        var approvedModulesByCefr = await _db.Modules.AsNoTracking()
            .Where(m => m.ReviewStatus == AdminReviewStatus.Approved && !m.IsArchived && m.CefrLevel != null)
            .GroupBy(m => m.CefrLevel!)
            .Select(g => new { CefrLevel = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CefrLevel, g => g.Count, ct);

        var bankCoverage = eligibleByCefr
            .Select(g => new PracticeGymDeliveryHealthBankCoverage(
                CefrLevel: g.CefrLevel,
                EligibleStudents: g.Count,
                ApprovedModuleCount: approvedModulesByCefr.GetValueOrDefault(g.CefrLevel, 0)))
            .OrderBy(b => Array.IndexOf(CefrLevelConstants.All.ToArray(), b.CefrLevel))
            .ToList();

        return Ok(new PracticeGymDeliveryHealthResult(todayResult, byCefrLevel, trend, topFallbackReasons, bankCoverage));
    }
}
