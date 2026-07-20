using System.Security.Claims;
using LinguaCoach.Application.ContentSeeding;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Adaptive Curriculum Sprint 6 — bulk content seeding. See <see cref="IContentSeedingService"/>
/// for the full rationale. A bounded, admin-triggered sweep — never automatic, never scheduled.
/// </summary>
[ApiController]
[Route("api/admin/content-seeding")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminContentSeedingController : ControllerBase
{
    private readonly IContentSeedingService _seeding;

    public AdminContentSeedingController(IContentSeedingService seeding) => _seeding = seeding;

    // POST api/admin/content-seeding/run
    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] RunContentSeedingRequestBody? body, CancellationToken ct)
    {
        var result = await _seeding.RunAsync(new ContentSeedingRequest(
            CefrLevels: body?.CefrLevels,
            MaxResourcesPerCefrLevelPerType: body?.MaxResourcesPerCefrLevelPerType ?? 3,
            ExercisesPerLesson: body?.ExercisesPerLesson ?? 2,
            CreatedByUserId: GetCurrentUserId()), ct);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

public sealed class RunContentSeedingRequestBody
{
    public IReadOnlyList<string>? CefrLevels { get; set; }
    public int? MaxResourcesPerCefrLevelPerType { get; set; }
    public int? ExercisesPerLesson { get; set; }
}
