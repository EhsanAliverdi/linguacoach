using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin read-only visibility into writing evaluation results and quality metrics.
/// Phase 17A — per-student listing.
/// Phase 17B — pipeline quality summary and per-evaluation dry-run signal preview.
/// No mutations. Dry-run signals never applied to mastery, CEFR, or Learning Plan.
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

    /// <summary>
    /// Returns pipeline-wide quality metrics and dry-run signal counts for all writing evaluations.
    /// Dry-run signals are never applied to mastery, CEFR, or Learning Plan progress.
    /// </summary>
    [HttpGet("/api/admin/writing-evaluation/quality-summary")]
    public async Task<ActionResult<WritingEvaluationQualitySummaryDto>> GetQualitySummary(
        CancellationToken ct = default)
    {
        var summary = await _query.GetQualitySummaryAsync(ct);
        return Ok(summary);
    }

    /// <summary>
    /// Returns a single writing evaluation combined with its computed dry-run signal preview.
    /// Dry-run signal is never applied to mastery, CEFR, or Learning Plan progress.
    /// </summary>
    [HttpGet("/api/admin/writing-evaluation/{id:guid}/dry-run")]
    public async Task<ActionResult<WritingEvaluationWithDryRunDto>> GetWithDryRun(
        Guid id, CancellationToken ct = default)
    {
        var result = await _query.GetWithDryRunAsync(id, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
