using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin read-only view of a student's Learning Plan / Journey state.
/// Reuses ILearningPlanService so the data matches what the student sees.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminStudentLearningPlanController : ControllerBase
{
    private readonly ILearningPlanService _learningPlan;

    public AdminStudentLearningPlanController(ILearningPlanService learningPlan)
    {
        _learningPlan = learningPlan;
    }

    /// <summary>
    /// Returns the learning plan journey for a student: CEFR level, phase,
    /// objective counts, current objective, completion percentage, and milestones.
    /// Returns a graceful empty result (planStatus=None) when no plan exists.
    /// </summary>
    [HttpGet("api/admin/students/{studentId:guid}/learning-plan-progress")]
    public async Task<IActionResult> GetLearningPlanProgress(Guid studentId, CancellationToken ct)
    {
        try
        {
            var result = await _learningPlan.GetJourneyAsync(studentId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }
}
