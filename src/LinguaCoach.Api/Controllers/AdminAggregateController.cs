using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminAggregateController : ControllerBase
{
    private readonly IAdminDashboardAggregateHandler _handler;

    public AdminAggregateController(IAdminDashboardAggregateHandler handler)
    {
        _handler = handler;
    }

    [HttpGet("dashboard/activity-trends")]
    public async Task<IActionResult> GetActivityTrends(
        [FromQuery] string period = "30d",
        CancellationToken ct = default)
    {
        var result = await _handler.GetActivityTrendsAsync(period, ct);
        return Ok(result);
    }

    [HttpGet("dashboard/score-distribution")]
    public async Task<IActionResult> GetScoreDistribution(
        [FromQuery] string period = "30d",
        CancellationToken ct = default)
    {
        var result = await _handler.GetScoreDistributionAsync(period, ct);
        return Ok(result);
    }

    [HttpGet("ai-usage/aggregate-trends")]
    public async Task<IActionResult> GetAiUsageTrends(
        [FromQuery] string period = "30d",
        CancellationToken ct = default)
    {
        var result = await _handler.GetAiUsageTrendsAsync(period, ct);
        return Ok(result);
    }

    [HttpGet("ai-usage/by-category")]
    public async Task<IActionResult> GetAiUsageCategoryBreakdown(
        [FromQuery] string period = "30d",
        CancellationToken ct = default)
    {
        var result = await _handler.GetAiUsageCategoryBreakdownAsync(period, ct);
        return Ok(result);
    }
}
