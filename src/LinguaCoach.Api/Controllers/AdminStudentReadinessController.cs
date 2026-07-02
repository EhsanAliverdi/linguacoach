using System.Security.Claims;
using LinguaCoach.Application.Admin.StudentReadiness;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase 20D: read-only student pilot-readiness audit + explicit, audited repair actions.
/// Admin-only. Never returns secrets, raw AI payloads, or another student's data.
/// </summary>
[ApiController]
[Route("api/admin/students/{studentId:guid}/readiness")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminStudentReadinessController : ControllerBase
{
    private readonly IStudentReadinessAuditService _auditService;
    private readonly IStudentPilotReadinessRepairService _repairService;

    public AdminStudentReadinessController(
        IStudentReadinessAuditService auditService,
        IStudentPilotReadinessRepairService repairService)
    {
        _auditService = auditService;
        _repairService = repairService;
    }

    private Guid AdminUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("No admin user id in token."));

    [HttpGet]
    public async Task<IActionResult> GetReadiness(Guid studentId, CancellationToken ct)
    {
        var summary = await _auditService.GetReadinessAsync(studentId, ct);
        if (summary is null) return NotFound(new { error = $"Unknown student profile '{studentId}'." });
        return Ok(summary);
    }

    [HttpPost("repair")]
    public async Task<IActionResult> Repair(Guid studentId, [FromBody] StudentReadinessRepairRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await _repairService.RepairAsync(studentId, AdminUserId, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("repair-safe-all")]
    public async Task<IActionResult> RepairSafeAll(
        Guid studentId, [FromBody] StudentReadinessRepairRequestDto request, CancellationToken ct)
    {
        try
        {
            var results = await _repairService.RunAllSafeRepairsAsync(studentId, AdminUserId, request.Reason, request.DryRun, ct);
            return Ok(results);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
