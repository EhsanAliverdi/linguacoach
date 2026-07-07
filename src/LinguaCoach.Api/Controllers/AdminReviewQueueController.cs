using LinguaCoach.Application.Admin.ReviewQueue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase 9 of the AI bank-first teaching architecture — a single cross-entity list of bank
/// content awaiting admin review. Read-only: approve/reject actions stay on each entity's own
/// controller (AdminActivityTemplateController/AdminPlacementItemController) — this endpoint
/// only helps an admin find what needs attention.
/// </summary>
[ApiController]
[Route("api/admin/review-queue")]
[Authorize(Roles = "Admin")]
public sealed class AdminReviewQueueController : ControllerBase
{
    private readonly IAdminReviewQueueQuery _query;

    public AdminReviewQueueController(IAdminReviewQueueQuery query)
    {
        _query = query;
    }

    // GET api/admin/review-queue?page=1&pageSize=20&entityType=ActivityTemplate&reviewStatus=PendingReview
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? entityType = null, [FromQuery] string? reviewStatus = "PendingReview",
        CancellationToken ct = default)
    {
        var result = await _query.HandleAsync(new ListAdminReviewQueueQuery(page, pageSize, entityType, reviewStatus), ct);
        return Ok(result);
    }
}
