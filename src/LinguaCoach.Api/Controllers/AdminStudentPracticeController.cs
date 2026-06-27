using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin read-only view of a student's Practice Gym state.
/// Surfaces review queue, weakest skill, top suggestion, and reserved count.
/// No write operations — practice state is managed by the suggestion service.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminStudentPracticeController : ControllerBase
{
    private readonly IAdminStudentPracticeQuery _practiceQuery;

    public AdminStudentPracticeController(IAdminStudentPracticeQuery practiceQuery)
    {
        _practiceQuery = practiceQuery;
    }

    /// <summary>
    /// Returns the practice summary for a student: review queue, weakest skill,
    /// top suggestion with reason, reserved count, and replenishment status.
    /// Returns Status=NotAvailable when the practice service is unavailable for the student.
    /// </summary>
    [HttpGet("api/admin/students/{studentId:guid}/practice-summary")]
    public async Task<IActionResult> GetPracticeSummary(Guid studentId, CancellationToken ct)
    {
        var result = await _practiceQuery.HandleAsync(new AdminStudentPracticeQuery(studentId), ct);
        return Ok(result);
    }
}
