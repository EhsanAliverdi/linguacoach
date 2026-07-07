using LinguaCoach.Application.Placement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/placement-items")]
[Authorize(Roles = "Admin")]
public sealed class AdminPlacementItemController : ControllerBase
{
    private readonly IAdminPlacementItemListQuery _listQuery;
    private readonly IAdminPlacementItemGetQuery _getQuery;
    private readonly IAdminAddPlacementItemHandler _addItem;
    private readonly IAdminUpdatePlacementItemHandler _updateItem;
    private readonly IAdminRemovePlacementItemHandler _removeItem;
    private readonly IAdminPlacementItemReviewHandler _reviewItem;
    private readonly IAdminPlacementItemCalibrationHandler _calibrationItem;

    public AdminPlacementItemController(
        IAdminPlacementItemListQuery listQuery,
        IAdminPlacementItemGetQuery getQuery,
        IAdminAddPlacementItemHandler addItem,
        IAdminUpdatePlacementItemHandler updateItem,
        IAdminRemovePlacementItemHandler removeItem,
        IAdminPlacementItemReviewHandler reviewItem,
        IAdminPlacementItemCalibrationHandler calibrationItem)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _addItem = addItem;
        _updateItem = updateItem;
        _removeItem = removeItem;
        _reviewItem = reviewItem;
        _calibrationItem = calibrationItem;
    }

    // GET api/admin/placement-items?page=1&pageSize=20&skill=grammar&search=turn+left
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? skill = null,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(new ListAdminPlacementItemsQuery(page, pageSize, skill, search), ct);
        return Ok(result);
    }

    // GET api/admin/placement-items/{itemId}
    [HttpGet("{itemId:guid}")]
    public async Task<IActionResult> Get(Guid itemId, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetAdminPlacementItemQuery(itemId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/placement-items
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] PlacementItemRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _addItem.HandleAsync(new AddPlacementItemCommand(
                request.Skill, request.CefrLevel, request.ItemOrder, request.IsEnabled,
                request.FormIoSchemaJson, request.ScoringRulesJson, request.RendererKind ?? "FormIo",
                request.AuthoringSchemaJson, request.DifficultyBand ?? 1, request.EvidenceWeight ?? 1.0), ct);
            return Ok(result);
        }
        catch (PlacementItemValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/placement-items/{itemId}
    [HttpPut("{itemId:guid}")]
    public async Task<IActionResult> Update(Guid itemId, [FromBody] PlacementItemRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _updateItem.HandleAsync(new UpdatePlacementItemCommand(
                itemId, request.Skill, request.CefrLevel, request.ItemOrder, request.IsEnabled,
                request.FormIoSchemaJson, request.ScoringRulesJson, request.RendererKind ?? "FormIo",
                request.AuthoringSchemaJson, request.DifficultyBand, request.EvidenceWeight), ct);
            return Ok(result);
        }
        catch (PlacementItemValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // DELETE api/admin/placement-items/{itemId}
    [HttpDelete("{itemId:guid}")]
    public async Task<IActionResult> Remove(Guid itemId, CancellationToken ct)
    {
        try
        {
            await _removeItem.HandleAsync(new RemovePlacementItemCommand(itemId), ct);
            return NoContent();
        }
        catch (PlacementItemValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/placement-items/{itemId}/review  { action: "approve"|"reject"|"reset", reason? }
    [HttpPost("{itemId:guid}/review")]
    public async Task<IActionResult> SetReviewStatus(Guid itemId, [FromBody] PlacementItemReviewRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _reviewItem.HandleAsync(new SetPlacementItemReviewStatusCommand(itemId, request.Action, request.Reason), ct);
            return Ok(result);
        }
        catch (PlacementItemValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/placement-items/{itemId}/calibration  { discriminationIndex?, calibrationSampleSize? }
    [HttpPost("{itemId:guid}/calibration")]
    public async Task<IActionResult> SetCalibrationStats(Guid itemId, [FromBody] PlacementItemCalibrationRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _calibrationItem.HandleAsync(
                new SetPlacementItemCalibrationStatsCommand(itemId, request.DiscriminationIndex, request.CalibrationSampleSize), ct);
            return Ok(result);
        }
        catch (PlacementItemValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Request model ────────────────────────────────────────────────────────

    public sealed record PlacementItemRequest(
        string Skill,
        string CefrLevel,
        int ItemOrder,
        bool IsEnabled,
        string FormIoSchemaJson,
        string ScoringRulesJson,
        string? RendererKind = null,
        string? AuthoringSchemaJson = null,
        int? DifficultyBand = null,
        double? EvidenceWeight = null);

    public sealed record PlacementItemReviewRequest(string Action, string? Reason = null);

    public sealed record PlacementItemCalibrationRequest(double? DiscriminationIndex, int? CalibrationSampleSize);
}
