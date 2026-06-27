using System.Security.Claims;
using LinguaCoach.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Consolidated student dashboard summary endpoint.
/// Returns all sections needed by the dashboard in a single request.
/// See: docs/sprints/current-sprint.md (Phase 15B)
/// </summary>
[ApiController]
[Route("api/student/dashboard")]
[Authorize]
public sealed class StudentDashboardController : ControllerBase
{
    private readonly IStudentDashboardSummaryHandler _handler;

    public StudentDashboardController(IStudentDashboardSummaryHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Returns the consolidated dashboard summary for the authenticated student.
    /// Missing optional sections return graceful states (Preparing/NotAvailable), not 500.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _handler.HandleAsync(new StudentDashboardSummaryQuery(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("onboarding"))
        {
            return StatusCode(403, new { error = ex.Message });
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
