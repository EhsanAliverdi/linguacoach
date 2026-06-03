using System.Security.Claims;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/activity")]
[Authorize]
public sealed class ActivityController : ControllerBase
{
    private readonly IGetNextActivityHandler _getNextActivity;
    private readonly ISubmitActivityAttemptHandler _submitAttempt;

    public ActivityController(
        IGetNextActivityHandler getNextActivity,
        ISubmitActivityAttemptHandler submitAttempt)
    {
        _getNextActivity = getNextActivity;
        _submitAttempt = submitAttempt;
    }

    /// <summary>
    /// Returns the next recommended activity for the student.
    /// Primary: AI-generated. Fallback: SystemFallback from seed data.
    /// Never returns 500 — if AI fails, a fallback activity is returned.
    /// </summary>
    [HttpGet("next")]
    [EnableRateLimiting("WritingAi")]
    public async Task<IActionResult> GetNext(
        [FromQuery] ActivityType? type = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _getNextActivity.HandleAsync(new GetNextActivityQuery(userId, type), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Submits a student attempt for AI evaluation.
    /// If AI evaluation fails, the attempt is still saved with empty feedback (no data loss).
    /// </summary>
    [HttpPost("{activityId:guid}/attempt")]
    [EnableRateLimiting("WritingAi")]
    public async Task<IActionResult> SubmitAttempt(
        Guid activityId,
        [FromBody] SubmitAttemptRequest request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.SubmittedContent))
            return BadRequest(new { error = "SubmittedContent is required." });

        try
        {
            var result = await _submitAttempt.HandleAsync(
                new SubmitActivityAttemptCommand(userId, activityId, request.SubmittedContent, request.AudioUrl),
                ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record SubmitAttemptRequest(string SubmittedContent, string? AudioUrl = null);
