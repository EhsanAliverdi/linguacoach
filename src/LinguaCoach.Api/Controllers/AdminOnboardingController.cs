using LinguaCoach.Application.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/onboarding")]
[Authorize(Roles = "Admin")]
public sealed class AdminOnboardingController : ControllerBase
{
    private readonly IAdminOnboardingFlowQuery _flowQuery;

    public AdminOnboardingController(IAdminOnboardingFlowQuery flowQuery)
    {
        _flowQuery = flowQuery;
    }

    [HttpGet("flow")]
    public async Task<IActionResult> GetFlow(CancellationToken ct)
    {
        var result = await _flowQuery.HandleAsync(new GetAdminOnboardingFlowQuery(), ct);
        if (result is null) return NotFound(new { error = "No active onboarding flow found." });
        return Ok(result);
    }
}
