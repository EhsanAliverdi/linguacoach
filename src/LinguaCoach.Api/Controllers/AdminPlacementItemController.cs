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
    private readonly IAdminAddPlacementItemHandler _addItem;
    private readonly IAdminUpdatePlacementItemHandler _updateItem;
    private readonly IAdminRemovePlacementItemHandler _removeItem;

    public AdminPlacementItemController(
        IAdminPlacementItemListQuery listQuery,
        IAdminAddPlacementItemHandler addItem,
        IAdminUpdatePlacementItemHandler updateItem,
        IAdminRemovePlacementItemHandler removeItem)
    {
        _listQuery = listQuery;
        _addItem = addItem;
        _updateItem = updateItem;
        _removeItem = removeItem;
    }

    // GET api/admin/placement-items
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _listQuery.HandleAsync(new ListAdminPlacementItemsQuery(), ct);
        return Ok(result);
    }

    // POST api/admin/placement-items
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] PlacementItemRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _addItem.HandleAsync(new AddPlacementItemCommand(
                request.Skill, request.CefrLevel, request.ItemType, request.Prompt, request.ItemOrder, request.IsEnabled,
                request.FormIoSchemaJson, request.ScoringRulesJson, request.RendererKind ?? "FormIo"), ct);
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
                itemId, request.Skill, request.CefrLevel, request.ItemType, request.Prompt, request.ItemOrder, request.IsEnabled,
                request.FormIoSchemaJson, request.ScoringRulesJson, request.RendererKind ?? "FormIo"), ct);
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

    // ── Request model ────────────────────────────────────────────────────────

    public sealed record PlacementItemRequest(
        string Skill,
        string CefrLevel,
        string ItemType,
        string Prompt,
        int ItemOrder,
        bool IsEnabled,
        string FormIoSchemaJson,
        string ScoringRulesJson,
        string? RendererKind = null);
}
