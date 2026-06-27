using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminPlacementController : ControllerBase
{
    private readonly IPlacementAssessmentService _placement;
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<AdminPlacementController> _logger;

    public AdminPlacementController(
        IPlacementAssessmentService placement,
        LinguaCoachDbContext db,
        ILogger<AdminPlacementController> logger)
    {
        _placement = placement;
        _db = db;
        _logger = logger;
    }

    [HttpGet("api/admin/students/{studentId:guid}/placement/latest")]
    public async Task<IActionResult> GetLatestPlacement(Guid studentId, CancellationToken ct)
    {
        if (!await StudentExistsAsync(studentId, ct))
            return NotFound(new { error = $"Student {studentId} not found." });

        var result = await _placement.GetLatestAssessmentAsync(studentId, ct);
        if (result is null)
            return Ok(new { hasPlacement = false });

        return Ok(result);
    }

    [HttpGet("api/admin/students/{studentId:guid}/placement/history")]
    public async Task<IActionResult> GetPlacementHistory(Guid studentId, CancellationToken ct)
    {
        if (!await StudentExistsAsync(studentId, ct))
            return NotFound(new { error = $"Student {studentId} not found." });

        var history = await _placement.GetHistoryAsync(studentId, ct);
        return Ok(history);
    }

    [HttpPost("api/admin/students/{studentId:guid}/placement/start")]
    public async Task<IActionResult> StartPlacement(Guid studentId, CancellationToken ct)
    {
        if (!await StudentExistsAsync(studentId, ct))
            return NotFound(new { error = $"Student {studentId} not found." });

        try
        {
            var result = await _placement.StartAssessmentAsync(studentId, "admin", ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot start placement for student {StudentId}", studentId);
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("api/admin/students/{studentId:guid}/placement/{assessmentId:guid}/complete")]
    public async Task<IActionResult> CompletePlacement(Guid studentId, Guid assessmentId, CancellationToken ct)
    {
        if (!await StudentExistsAsync(studentId, ct))
            return NotFound(new { error = $"Student {studentId} not found." });

        var belongs = await _db.PlacementAssessments
            .AnyAsync(a => a.Id == assessmentId && a.StudentProfileId == studentId, ct);
        if (!belongs)
            return NotFound(new { error = $"Assessment {assessmentId} not found for student {studentId}." });

        try
        {
            var result = await _placement.CompleteAssessmentAsync(assessmentId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot complete placement {AssessmentId}", assessmentId);
            return Conflict(new { error = ex.Message });
        }
    }

    private Task<bool> StudentExistsAsync(Guid studentId, CancellationToken ct) =>
        _db.StudentProfiles.AnyAsync(p => p.Id == studentId, ct);
}
