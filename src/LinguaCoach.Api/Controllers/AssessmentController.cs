using System.Security.Claims;
using LinguaCoach.Application.Assessment;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/assessment")]
[Authorize]
public sealed class AssessmentController : ControllerBase
{
    private readonly ICefrAssessmentHandler _handler;

    public AssessmentController(ICefrAssessmentHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Submits a writing sample for CEFR level assessment. Calls the AI provider.</summary>
    [HttpPost("cefr")]
    public async Task<IActionResult> AssessCefr([FromBody] CefrAssessmentRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.StudentSample))
            return BadRequest(new { error = "Student sample text is required." });

        try
        {
            var result = await _handler.HandleAsync(
                new CefrAssessmentCommand(userId, request.StudentSample), ct);
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
        catch (AiProviderException ex)
        {
            return StatusCode(502, new { error = "AI service is temporarily unavailable.", detail = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record CefrAssessmentRequest(string StudentSample);
