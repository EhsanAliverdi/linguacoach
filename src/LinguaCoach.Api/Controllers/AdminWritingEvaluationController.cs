using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin read-only visibility into a student's writing evaluation results.
/// Phase 17A — foundation. No mutations. Never exposes raw provider payloads.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/students")]
public sealed class AdminWritingEvaluationController : ControllerBase
{
    private readonly IAdminWritingEvaluationQuery _query;

    public AdminWritingEvaluationController(IAdminWritingEvaluationQuery query) => _query = query;

    /// <summary>
    /// Returns all writing evaluations for the given student profile, newest first.
    /// </summary>
    [HttpGet("{studentId:guid}/writing-evaluations")]
    public async Task<ActionResult<IReadOnlyList<AdminWritingEvaluationItemDto>>> GetForStudent(
        Guid studentId, CancellationToken ct = default)
    {
        var items = await _query.GetForStudentAsync(studentId, ct);
        return Ok(items);
    }
}
