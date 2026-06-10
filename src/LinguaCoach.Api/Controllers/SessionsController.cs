using System.Security.Claims;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Student-facing session endpoints.
/// Authenticated students can only access their own sessions.
/// See: docs/sprints/2026-06-10-today-lesson-learning-session-sprint.md
/// </summary>
[ApiController]
[Route("api/sessions")]
[Authorize]
public sealed class SessionsController : ControllerBase
{
    private readonly IGetTodaysSessionHandler _today;
    private readonly IGetSessionHandler _get;
    private readonly IGetSessionHistoryHandler _history;
    private readonly IStartSessionHandler _start;
    private readonly ICompleteSessionHandler _complete;
    private readonly ICompleteExerciseHandler _completeExercise;
    private readonly IPrepareExerciseHandler _prepare;

    public SessionsController(
        IGetTodaysSessionHandler today,
        IGetSessionHandler get,
        IGetSessionHistoryHandler history,
        IStartSessionHandler start,
        ICompleteSessionHandler complete,
        ICompleteExerciseHandler completeExercise,
        IPrepareExerciseHandler prepare)
    {
        _today = today;
        _get = get;
        _history = history;
        _start = start;
        _complete = complete;
        _completeExercise = completeExercise;
        _prepare = prepare;
    }

    /// <summary>
    /// Returns today's session for the current student, creating it if necessary.
    /// Idempotent: safe to call multiple times per day.
    /// </summary>
    [HttpGet("today")]
    public async Task<IActionResult> Today(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _today.HandleAsync(new GetTodaysSessionQuery(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Returns the full detail of a session including all ordered exercises.</summary>
    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _get.HandleAsync(new GetSessionQuery(userId, sessionId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Marks a session as InProgress. Idempotent if already started.</summary>
    [HttpPost("{sessionId:guid}/start")]
    public async Task<IActionResult> Start(Guid sessionId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _start.HandleAsync(new StartSessionCommand(userId, sessionId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Marks a session as Completed. Idempotent if already completed.</summary>
    [HttpPost("{sessionId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid sessionId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _complete.HandleAsync(new CompleteSessionCommand(userId, sessionId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Marks an exercise step as Completed.
    /// Returns whether the session is now fully complete.
    /// </summary>
    [HttpPost("{sessionId:guid}/exercises/{exerciseId:guid}/complete")]
    public async Task<IActionResult> CompleteExercise(Guid sessionId, Guid exerciseId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _completeExercise.HandleAsync(
                new CompleteExerciseCommand(userId, sessionId, exerciseId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Generates (or retrieves) the LearningActivity for an exercise step.
    /// Idempotent: calling twice returns the same activity.
    /// Review steps return a lightweight reflection placeholder without AI generation.
    /// </summary>
    [HttpPost("{sessionId:guid}/exercises/{exerciseId:guid}/prepare")]
    public async Task<IActionResult> PrepareExercise(Guid sessionId, Guid exerciseId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _prepare.HandleAsync(
                new PrepareExerciseCommand(userId, sessionId, exerciseId), ct);
            return Ok(result);
        }
        catch (AiServiceUnavailableException ex)
        {
            return StatusCode(503, new { error = "The AI service is not available. Please try again shortly.", retryable = true, featureKey = ex.FeatureKey });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Returns paginated session history for the current student, newest first.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _history.HandleAsync(new GetSessionHistoryQuery(userId, page, pageSize), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returns an AI-generated reflection summary for the completed session.
    /// Not yet implemented — planned for Phase 4 (requires AI prompt + reflection service).
    /// </summary>
    [HttpGet("{sessionId:guid}/reflection")]
    public IActionResult Reflection(Guid sessionId)
        => StatusCode(501, new { error = "Session reflection is not yet implemented. Planned for Phase 4." });

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}
