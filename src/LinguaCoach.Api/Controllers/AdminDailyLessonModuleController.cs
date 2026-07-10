using LinguaCoach.Application.DailyLessonModules;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H6 — admin-only, read-only diagnostics for the Daily Lesson module pipeline: which
/// approved Modules would be (or were) selected for a student's Today, and why a fallback was
/// used when one was. The preview endpoint never mutates anything — it calls the selector
/// directly, bypassing the assignment recorder, so previewing has no side effects.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminDailyLessonModuleController : ControllerBase
{
    private readonly IDailyLessonModuleSelectionService _selector;
    private readonly LinguaCoachDbContext _db;

    public AdminDailyLessonModuleController(IDailyLessonModuleSelectionService selector, LinguaCoachDbContext db)
    {
        _selector = selector;
        _db = db;
    }

    /// <summary>
    /// Previews which approved Modules the deterministic selector would choose for a student
    /// today (or a given date), and the fallback reason if none would be chosen. Read-only —
    /// does not record a <c>StudentDailyModuleAssignment</c> row.
    /// </summary>
    [HttpGet("api/admin/daily-lesson/modules/preview")]
    public async Task<IActionResult> PreviewSelection(
        [FromQuery] Guid studentId, [FromQuery] int? maxModules, [FromQuery] DateTime? targetDate, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == studentId, ct);
        if (profile is null)
            return NotFound(new { error = "Student not found." });

        var result = await _selector.SelectAsync(
            new DailyLessonModuleSelectionRequest(
                StudentId: studentId,
                CefrLevel: profile.CefrLevel,
                LearningPlanId: null,
                TargetDate: (targetDate ?? DateTime.UtcNow).Date,
                MaxModules: maxModules ?? 1),
            ct);

        return Ok(result);
    }

    /// <summary>
    /// Returns recent Daily Lesson module assignment bookkeeping rows for a student
    /// (most recent first). A date with no row for it means the fallback path was used that
    /// day — see <c>StudentDailyModuleAssignmentRecorder</c>'s doc comment. Read-only.
    /// </summary>
    [HttpGet("api/admin/daily-lesson/students/{studentId:guid}/assignments")]
    public async Task<IActionResult> GetAssignmentHistory(Guid studentId, [FromQuery] int? days, CancellationToken ct)
    {
        var lookbackDays = days is > 0 and <= 365 ? days.Value : 30;
        var since = DateTime.UtcNow.Date.AddDays(-lookbackDays);

        var assignments = await _db.StudentDailyModuleAssignments
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
}
