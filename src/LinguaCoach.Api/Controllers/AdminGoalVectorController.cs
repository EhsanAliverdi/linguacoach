using LinguaCoach.Application.GoalVector;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Adaptive Curriculum Sprint 3 — admin trigger for the one-time, idempotent backfill of existing
/// students' old <c>LearningGoals</c> free-list onto the new weighted goal vector. See
/// <see cref="IStudentGoalVectorBackfillService"/>'s doc comment for the key-mapping rationale.
/// Safe to call more than once (never overwrites an existing weight).
/// </summary>
[ApiController]
[Route("api/admin/goal-vector")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminGoalVectorController : ControllerBase
{
    private readonly IStudentGoalVectorBackfillService _backfill;

    public AdminGoalVectorController(IStudentGoalVectorBackfillService backfill) => _backfill = backfill;

    [HttpPost("backfill-from-learning-goals")]
    public async Task<IActionResult> BackfillFromLearningGoals(CancellationToken ct)
    {
        var result = await _backfill.BackfillFromLearningGoalsAsync(ct);
        return Ok(result);
    }
}
