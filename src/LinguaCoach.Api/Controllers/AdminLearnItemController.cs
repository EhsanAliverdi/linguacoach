using System.Security.Claims;
using LinguaCoach.Application.LearnItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H3 — Learn Item foundation. Reviewable teaching/explanation blocks generated from (or
/// manually authored about) selected published Resource Bank rows — the "Learn" half of a future
/// Module. Every create/generate action stages a pending-review row; only <see cref="Approve"/>/
/// <see cref="Reject"/> change that. Never creates an Activity/Module row, never assigns anything
/// to a student.
/// </summary>
[ApiController]
[Route("api/admin/learn-items")]
[Authorize(Roles = "Admin")]
public sealed class AdminLearnItemController : ControllerBase
{
    private readonly IAdminLearnItemListQuery _listQuery;
    private readonly IAdminLearnItemGetQuery _getQuery;
    private readonly IAdminCreateLearnItemHandler _createHandler;
    private readonly IAdminUpdateLearnItemHandler _updateHandler;
    private readonly IAdminApproveLearnItemHandler _approveHandler;
    private readonly IAdminRejectLearnItemHandler _rejectHandler;
    private readonly IGenerateLearnItemFromResourcesHandler _generateHandler;

    public AdminLearnItemController(
        IAdminLearnItemListQuery listQuery,
        IAdminLearnItemGetQuery getQuery,
        IAdminCreateLearnItemHandler createHandler,
        IAdminUpdateLearnItemHandler updateHandler,
        IAdminApproveLearnItemHandler approveHandler,
        IAdminRejectLearnItemHandler rejectHandler,
        IGenerateLearnItemFromResourcesHandler generateHandler)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _generateHandler = generateHandler;
    }

    // GET api/admin/learn-items?page=&pageSize=&status=&cefrLevel=&skill=&subskill=&contextTag=&
    //   focusTag=&difficultyBand=&search=&resourceType=&resourceId=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null,
        [FromQuery] string? cefrLevel = null, [FromQuery] string? skill = null, [FromQuery] string? subskill = null,
        [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] int? difficultyBand = null, [FromQuery] string? search = null,
        [FromQuery] string? resourceType = null, [FromQuery] Guid? resourceId = null,
        CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(new ListLearnItemsQuery(
            page, pageSize, status, cefrLevel, skill, subskill, contextTag, focusTag,
            difficultyBand, search, resourceType, resourceId), ct);
        return Ok(result);
    }

    // GET api/admin/learn-items/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetLearnItemQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/learn-items
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLearnItemRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _createHandler.HandleAsync(new CreateLearnItemCommand(
                body.Title, body.Body, body.CefrLevel, body.Skill, body.Subskill,
                body.ContextTags, body.FocusTags, body.Examples, body.CommonMistakes,
                body.UsageNotes, body.DifficultyBand, body.EstimatedMinutes, body.Links, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (LearnItemValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/learn-items/generate-from-resources
    [HttpPost("generate-from-resources")]
    public async Task<IActionResult> GenerateFromResources(
        [FromBody] GenerateLearnItemFromResourcesRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateHandler.HandleAsync(new GenerateLearnItemFromResourcesRequest(
                body.Resources, body.Title, body.DefaultCefrLevel, body.DefaultSkill, body.DefaultSubskill,
                body.DefaultContextTags, body.DefaultFocusTags, body.DefaultDifficultyBand, body.Notes,
                GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (LearnItemValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/learn-items/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLearnItemRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _updateHandler.HandleAsync(new UpdateLearnItemCommand(
                id, body.Title, body.Body, body.Examples, body.CommonMistakes, body.UsageNotes,
                body.CefrLevel, body.Skill, body.Subskill, body.ContextTags, body.FocusTags,
                body.DifficultyBand, body.EstimatedMinutes), ct);
            return Ok(result);
        }
        catch (LearnItemValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/learn-items/{id}/approve  { notes? }
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveLearnItemRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _approveHandler.HandleAsync(new ApproveLearnItemCommand(id, GetCurrentUserId(), body.Notes), ct);
            return Ok(result);
        }
        catch (LearnItemValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/learn-items/{id}/reject  { reason }
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectLearnItemRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _rejectHandler.HandleAsync(new RejectLearnItemCommand(id, body.Reason, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (LearnItemValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public sealed record CreateLearnItemRequestBody(
        string Title, string Body, string? CefrLevel = null, string? Skill = null, string? Subskill = null,
        IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        IReadOnlyList<string>? Examples = null, IReadOnlyList<string>? CommonMistakes = null,
        string? UsageNotes = null, int? DifficultyBand = null, int? EstimatedMinutes = null,
        IReadOnlyList<LearnItemResourceLinkInput>? Links = null
    );

    public sealed record GenerateLearnItemFromResourcesRequestBody(
        IReadOnlyList<LearnItemResourceLinkInput> Resources,
        string? Title = null, string? DefaultCefrLevel = null, string? DefaultSkill = null,
        string? DefaultSubskill = null, IReadOnlyList<string>? DefaultContextTags = null,
        IReadOnlyList<string>? DefaultFocusTags = null, int? DefaultDifficultyBand = null,
        string? Notes = null
    );

    public sealed record UpdateLearnItemRequestBody(
        string Title, string Body, IReadOnlyList<string>? Examples = null, IReadOnlyList<string>? CommonMistakes = null,
        string? UsageNotes = null, string? CefrLevel = null, string? Skill = null, string? Subskill = null,
        IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null
    );

    public sealed record ApproveLearnItemRequestBody(string? Notes = null);
    public sealed record RejectLearnItemRequestBody(string Reason);
}
