using LinguaCoach.Application.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public sealed class AdminStudentProgressController : ControllerBase
{
    private readonly IAdminStudentProgressQuery _query;

    public AdminStudentProgressController(IAdminStudentProgressQuery query) => _query = query;

    [HttpGet("api/admin/students/{studentId:guid}/progress-summary")]
    public async Task<IActionResult> Get(Guid studentId, CancellationToken ct)
    {
        var result = await _query.HandleAsync(new AdminStudentProgressQuery(studentId), ct);
        if (result == null) return NotFound();
        return Ok(new
        {
            currentCefrLevel = result.CurrentCefrLevel,
            placementCefrLevel = result.PlacementCefrLevel,
            placementCompletedAt = result.PlacementCompletedAt,
            masteredObjectivesCount = result.MasteredObjectivesCount,
            inProgressObjectivesCount = result.InProgressObjectivesCount,
            reviewQueueCount = result.ReviewQueueCount,
            totalObjectives = result.TotalObjectives,
            completionPercentage = result.CompletionPercentage,
            strongestSkill = result.StrongestSkill,
            weakestSkill = result.WeakestSkill,
            weakSkillsCount = result.WeakSkillsCount,
            lastLearningActivityAt = result.LastLearningActivityAt,
            currentLearningPhase = result.CurrentLearningPhase,
        });
    }
}
