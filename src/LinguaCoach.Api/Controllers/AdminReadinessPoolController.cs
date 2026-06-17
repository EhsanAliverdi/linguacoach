using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Read-only admin endpoint for inspecting a student's activity readiness pool.
/// No write endpoints — pool state is managed by the pool service and generation jobs.
/// </summary>
[ApiController]
[Route("api/admin/students")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminReadinessPoolController : ControllerBase
{
    private readonly IStudentActivityReadinessPoolService _poolService;

    public AdminReadinessPoolController(IStudentActivityReadinessPoolService poolService)
    {
        _poolService = poolService;
    }

    /// <summary>Returns the readiness pool summary and items for a student.</summary>
    [HttpGet("{studentId:guid}/readiness-pool")]
    public async Task<IActionResult> GetReadinessPool(Guid studentId, CancellationToken ct)
    {
        var summary = await _poolService.GetPoolSummaryAsync(studentId, ct);
        return Ok(summary);
    }
}
