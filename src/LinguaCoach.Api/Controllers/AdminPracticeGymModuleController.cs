using LinguaCoach.Application.PracticeGymModules;
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
            .Where(a => a.ModuleDefinitionId.HasValue)
            .Select(a => a.ModuleDefinitionId!.Value)
            .Distinct()
            .ToList();

        var titlesById = await _db.ModuleDefinitions
            .AsNoTracking()
            .Where(m => moduleIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Title })
            .ToDictionaryAsync(m => m.Id, m => m.Title, ct);

        var items = assignments.Select(a => new
        {
            a.Id,
            a.SuggestedAt,
            ModuleDefinitionId = a.ModuleDefinitionId,
            ModuleTitle = a.ModuleDefinitionId.HasValue ? titlesById.GetValueOrDefault(a.ModuleDefinitionId.Value) : null,
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
}
