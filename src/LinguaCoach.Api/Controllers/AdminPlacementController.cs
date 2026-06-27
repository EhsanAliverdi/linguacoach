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

        var belongs = await AssessmentBelongsAsync(studentId, assessmentId, ct);
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

    // Phase 13B — detailed progress for admin

    [HttpGet("api/admin/students/{studentId:guid}/placement/{assessmentId:guid}/progress")]
    public async Task<IActionResult> GetPlacementProgress(Guid studentId, Guid assessmentId, CancellationToken ct)
    {
        if (!await StudentExistsAsync(studentId, ct))
            return NotFound(new { error = $"Student {studentId} not found." });

        if (!await AssessmentBelongsAsync(studentId, assessmentId, ct))
            return NotFound(new { error = $"Assessment {assessmentId} not found for student {studentId}." });

        try
        {
            var progress = await _placement.GetProgressAsync(assessmentId, ct);
            return Ok(progress);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("api/admin/students/{studentId:guid}/placement/{assessmentId:guid}/items")]
    public async Task<IActionResult> GetPlacementItems(Guid studentId, Guid assessmentId, CancellationToken ct)
    {
        if (!await StudentExistsAsync(studentId, ct))
            return NotFound(new { error = $"Student {studentId} not found." });

        if (!await AssessmentBelongsAsync(studentId, assessmentId, ct))
            return NotFound(new { error = $"Assessment {assessmentId} not found for student {studentId}." });

        try
        {
            var progress = await _placement.GetProgressAsync(assessmentId, ct);
            return Ok(progress.ItemHistory);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // Phase 13B — submit a response (admin-only, for testing/QA)

    [HttpPost("api/admin/students/{studentId:guid}/placement/{assessmentId:guid}/items/{itemId:guid}/submit")]
    public async Task<IActionResult> SubmitResponse(
        Guid studentId, Guid assessmentId, Guid itemId,
        [FromBody] SubmitPlacementResponseRequest request,
        CancellationToken ct)
    {
        if (!await StudentExistsAsync(studentId, ct))
            return NotFound(new { error = $"Student {studentId} not found." });

        if (!await AssessmentBelongsAsync(studentId, assessmentId, ct))
            return NotFound(new { error = $"Assessment {assessmentId} not found for student {studentId}." });

        if (string.IsNullOrWhiteSpace(request?.Response))
            return BadRequest(new { error = "Response is required." });

        try
        {
            var result = await _placement.SubmitResponseAsync(
                assessmentId, itemId, request.Response, request.DurationSeconds, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot submit response for item {ItemId}", itemId);
            return Conflict(new { error = ex.Message });
        }
    }

    private Task<bool> StudentExistsAsync(Guid studentId, CancellationToken ct) =>
        _db.StudentProfiles.AnyAsync(p => p.Id == studentId, ct);

    private Task<bool> AssessmentBelongsAsync(Guid studentId, Guid assessmentId, CancellationToken ct) =>
        _db.PlacementAssessments.AnyAsync(a => a.Id == assessmentId && a.StudentProfileId == studentId, ct);
}

public sealed record SubmitPlacementResponseRequest(string Response, int? DurationSeconds);
