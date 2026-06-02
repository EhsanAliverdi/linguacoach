using System.Security.Claims;
using LinguaCoach.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardQueryHandler _handler;

    public DashboardController(IDashboardQueryHandler handler)
    {
        _handler = handler;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _handler.HandleAsync(new DashboardQuery(userId), ct);
            return Ok(new
            {
                studentName = result.StudentName,
                careerProfile = result.CareerProfileName,
                cefrLevel = result.CefrLevel,
                message = result.Message
            });
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
