using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin read-only visibility into writing evaluation results and quality metrics.
/// Phase 17A — per-student listing.
/// Phase 17B — pipeline quality summary and per-evaluation dry-run signal preview.
/// Phase 17C — applied signal summary and safety invariant verification.
/// No mutations. Signals never update CEFR, complete objectives, or regenerate Learning Plan.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/students")]
public sealed class AdminWritingEvaluationController : ControllerBase
{
    private readonly IAdminWritingEvaluationQuery _query;
    private readonly IWritingEvaluationSignalApplicationService _signalService;

    public AdminWritingEvaluationController(
        IAdminWritingEvaluationQuery query,
        IWritingEvaluationSignalApplicationService signalService)
    {
        _query = query;
        _signalService = signalService;
    }

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

    /// <summary>
    /// Returns applied signal counts, config status, and blocked breakdowns.
    /// CEFR updates, objective completions, and Learning Plan regeneration are permanently disabled.
    /// </summary>
    [HttpGet("/api/admin/writing-evaluation/applied-signals-summary")]
    public async Task<ActionResult<WritingSignalApplicationSummaryDto>> GetAppliedSignalsSummary(
        CancellationToken ct = default)
    {
        var summary = await _signalService.GetSummaryAsync(ct);
        return Ok(summary);
    }

    /// <summary>
    /// Returns invariant safety verification confirming structural safety rules are in effect.
    /// Confirms CEFR updates, objective completions, and LP auto-regen are permanently off.
    /// </summary>
    [HttpGet("/api/admin/writing-evaluation/signal-safety-summary")]
    public async Task<ActionResult<WritingSignalSafetySummaryDto>> GetSignalSafetySummary(
        CancellationToken ct = default)
    {
        var summary = await _signalService.GetSignalSafetySummaryAsync(ct);
        return Ok(summary);
    }
}
