using System.Security.Claims;
using LinguaCoach.Application.LearningPlan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Student-facing Learning Plan endpoints.
/// Phase 15E: Journey view model for the Learning Journey page.
/// </summary>
[ApiController]
[Authorize]
public sealed class StudentLearningPlanController : ControllerBase
{
    private readonly ILearningPlanService _learningPlan;

    public StudentLearningPlanController(ILearningPlanService learningPlan)
    {
        _learningPlan = learningPlan;
    }

    /// <summary>
    /// Returns the full Learning Journey for the authenticated student.
    /// Groups objectives by status: current, upcoming, completed, review.
    /// Returns a graceful empty result when no plan exists — never 500.
    /// </summary>
    [HttpGet("api/student/learning-plan/journey")]
    public async Task<IActionResult> GetJourney(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var journey = await _learningPlan.GetJourneyForUserAsync(userId, ct);
            return Ok(journey);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
            out var id) ? id : Guid.Empty;
}
