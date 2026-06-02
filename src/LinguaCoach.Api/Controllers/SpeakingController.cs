using System.Security.Claims;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/speaking")]
[Authorize]
public sealed class SpeakingController : ControllerBase
{
    private readonly ICreateSpeakingSessionHandler _createSession;
    private readonly ISubmitSpeakingTurnHandler _submitTurn;

    public SpeakingController(
        ICreateSpeakingSessionHandler createSession,
        ISubmitSpeakingTurnHandler submitTurn)
    {
        _createSession = createSession;
        _submitTurn = submitTurn;
    }

    /// <summary>Creates a new speaking session from a scenario. Calls AI for the opening question.</summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _createSession.HandleAsync(
                new CreateSpeakingSessionCommand(userId, request.ScenarioId), ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (AiProviderException ex) { return StatusCode(502, new { error = "AI service unavailable.", detail = ex.Message }); }
    }

    /// <summary>Submits a transcript for the current open turn. Calls AI. Completes session on last turn.</summary>
    [HttpPost("sessions/{sessionId:guid}/turns")]
    public async Task<IActionResult> SubmitTurn(Guid sessionId, [FromBody] SubmitTurnRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.UserTranscript))
            return BadRequest(new { error = "User transcript is required." });

        try
        {
            var result = await _submitTurn.HandleAsync(
                new SubmitSpeakingTurnCommand(userId, sessionId, request.UserTranscript), ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (AiProviderException ex) { return StatusCode(502, new { error = "AI service unavailable.", detail = ex.Message }); }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record CreateSessionRequest(Guid ScenarioId);
public sealed record SubmitTurnRequest(string UserTranscript);
