using System.Security.Claims;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Writing;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/writing")]
[Authorize]
public sealed class WritingExerciseController : ControllerBase
{
    private const string AiUnavailableMessage = "AI feedback is not configured or is temporarily unavailable.";

    private readonly IGetWritingExerciseHandler _getExercise;
    private readonly ISubmitWritingDraftHandler _submitDraft;

    public WritingExerciseController(
        IGetWritingExerciseHandler getExercise,
        ISubmitWritingDraftHandler submitDraft)
    {
        _getExercise = getExercise;
        _submitDraft = submitDraft;
    }

    /// <summary>Returns the current writing exercise scenario without calling AI.</summary>
    [HttpGet("exercise")]
    public async Task<IActionResult> GetExercise(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _getExercise.HandleAsync(new GetWritingExerciseQuery(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Submits a draft email for AI feedback. Calls the AI provider.</summary>
    [HttpPost("exercise/submit")]
    [EnableRateLimiting("WritingAi")]
    public async Task<IActionResult> Submit([FromBody] SubmitDraftRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.DraftText))
            return BadRequest(new { error = "Draft text is required." });

        try
        {
            var result = await _submitDraft.HandleAsync(
                new SubmitWritingDraftCommand(userId, request.DraftText), ct);
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
        catch (AiConfigurationUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "ai_unavailable",
                error = AiUnavailableMessage,
                detail = ex.Message
            });
        }
        catch (AiResponseValidationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                code = "ai_validation_failed",
                error = AiUnavailableMessage,
                detail = ex.Message
            });
        }
        catch (AiProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                code = "ai_unavailable",
                error = AiUnavailableMessage,
                detail = ex.Message
            });
        }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record SubmitDraftRequest(string DraftText);
