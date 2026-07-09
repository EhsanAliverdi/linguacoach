using System.Security.Claims;
using LinguaCoach.Application.ModuleDefinitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H5 — Module Definition foundation. Reusable, reviewable learning units combining one or
/// more Learn Items and Activity Definitions plus a module-level feedback plan — the top of the
/// content-studio hierarchy (Resource Bank Item → Learn Item/Activity Definition → Module
/// Definition). Distinct from the existing runtime <c>LearningModule</c> (a per-student thematic
/// group within a LearningPath) — see <c>ModuleDefinition</c>'s doc comment. Every create/generate
/// action stages a pending-review row; only <see cref="Approve"/>/<see cref="Reject"/> change
/// that. Never assigns anything to a student, never changes Today/Practice Gym runtime selection.
/// </summary>
[ApiController]
[Route("api/admin/modules")]
[Authorize(Roles = "Admin")]
public sealed class AdminModuleDefinitionController : ControllerBase
{
    private readonly IAdminModuleDefinitionListQuery _listQuery;
    private readonly IAdminModuleDefinitionGetQuery _getQuery;
    private readonly IAdminCreateModuleDefinitionHandler _createHandler;
    private readonly IAdminUpdateModuleDefinitionHandler _updateHandler;
    private readonly IAdminApproveModuleDefinitionHandler _approveHandler;
    private readonly IAdminRejectModuleDefinitionHandler _rejectHandler;
    private readonly IGenerateModuleFromItemsHandler _generateFromItemsHandler;
    private readonly IGenerateModuleFromResourceHandler _generateFromResourceHandler;
    private readonly IGenerateModuleFromLearnItemHandler _generateFromLearnItemHandler;
    private readonly IGenerateModuleFromActivityHandler _generateFromActivityHandler;

    public AdminModuleDefinitionController(
        IAdminModuleDefinitionListQuery listQuery,
        IAdminModuleDefinitionGetQuery getQuery,
        IAdminCreateModuleDefinitionHandler createHandler,
        IAdminUpdateModuleDefinitionHandler updateHandler,
        IAdminApproveModuleDefinitionHandler approveHandler,
        IAdminRejectModuleDefinitionHandler rejectHandler,
        IGenerateModuleFromItemsHandler generateFromItemsHandler,
        IGenerateModuleFromResourceHandler generateFromResourceHandler,
        IGenerateModuleFromLearnItemHandler generateFromLearnItemHandler,
        IGenerateModuleFromActivityHandler generateFromActivityHandler)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _generateFromItemsHandler = generateFromItemsHandler;
        _generateFromResourceHandler = generateFromResourceHandler;
        _generateFromLearnItemHandler = generateFromLearnItemHandler;
        _generateFromActivityHandler = generateFromActivityHandler;
    }

    // GET api/admin/modules?page=&pageSize=&status=&cefrLevel=&skill=&subskill=&contextTag=&
    //   focusTag=&difficultyBand=&learnItemId=&activityDefinitionId=&search=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null,
        [FromQuery] string? cefrLevel = null, [FromQuery] string? skill = null, [FromQuery] string? subskill = null,
        [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] int? difficultyBand = null, [FromQuery] Guid? learnItemId = null,
        [FromQuery] Guid? activityDefinitionId = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(new ListModuleDefinitionsQuery(
            page, pageSize, status, cefrLevel, skill, subskill, contextTag, focusTag,
            difficultyBand, learnItemId, activityDefinitionId, search), ct);
        return Ok(result);
    }

    // GET api/admin/modules/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetModuleDefinitionQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/modules
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateModuleDefinitionRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _createHandler.HandleAsync(new CreateModuleDefinitionCommand(
                body.Title, body.LearnItemLinks, body.ActivityLinks, body.Description, body.ObjectiveKey,
                body.CefrLevel, body.Skill, body.Subskill, body.ContextTags, body.FocusTags,
                body.DifficultyBand, body.EstimatedMinutes, body.FeedbackPlanJson, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-items
    [HttpPost("generate-from-items")]
    public async Task<IActionResult> GenerateFromItems([FromBody] GenerateModuleFromItemsRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromItemsHandler.HandleAsync(new GenerateModuleFromItemsRequest(
                body.LearnItemLinks, body.ActivityLinks, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-resource
    [HttpPost("generate-from-resource")]
    public async Task<IActionResult> GenerateFromResource([FromBody] GenerateModuleFromResourceRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromResourceHandler.HandleAsync(new GenerateModuleFromResourceRequest(
                body.ResourceType, body.ResourceId, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-learn-item
    [HttpPost("generate-from-learn-item")]
    public async Task<IActionResult> GenerateFromLearnItem([FromBody] GenerateModuleFromLearnItemRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromLearnItemHandler.HandleAsync(new GenerateModuleFromLearnItemRequest(
                body.LearnItemId, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-activity
    [HttpPost("generate-from-activity")]
    public async Task<IActionResult> GenerateFromActivity([FromBody] GenerateModuleFromActivityRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromActivityHandler.HandleAsync(new GenerateModuleFromActivityRequest(
                body.ActivityDefinitionId, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/modules/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModuleDefinitionRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _updateHandler.HandleAsync(new UpdateModuleDefinitionCommand(
                id, body.Title, body.Description, body.CefrLevel, body.Skill, body.Subskill,
                body.ContextTags, body.FocusTags, body.DifficultyBand, body.EstimatedMinutes, body.FeedbackPlanJson), ct);
            return Ok(result);
        }
        catch (ModuleDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/{id}/approve  { notes? }
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveModuleDefinitionRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _approveHandler.HandleAsync(new ApproveModuleDefinitionCommand(id, GetCurrentUserId(), body.Notes), ct);
            return Ok(result);
        }
        catch (ModuleDefinitionValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/{id}/reject  { reason }
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectModuleDefinitionRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _rejectHandler.HandleAsync(new RejectModuleDefinitionCommand(id, body.Reason, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public sealed record CreateModuleDefinitionRequestBody(
        string Title, IReadOnlyList<ModuleLearnItemLinkInput> LearnItemLinks, IReadOnlyList<ModuleActivityLinkInput> ActivityLinks,
        string? Description = null, string? ObjectiveKey = null, string? CefrLevel = null, string? Skill = null,
        string? Subskill = null, IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null, string? FeedbackPlanJson = null
    );

    public sealed record GenerateModuleFromItemsRequestBody(
        IReadOnlyList<ModuleLearnItemLinkInput> LearnItemLinks, IReadOnlyList<ModuleActivityLinkInput> ActivityLinks,
        string? Title = null, string? Notes = null
    );

    public sealed record GenerateModuleFromResourceRequestBody(
        string ResourceType, Guid ResourceId, string? Title = null, string? Notes = null
    );

    public sealed record GenerateModuleFromLearnItemRequestBody(
        Guid LearnItemId, string? Title = null, string? Notes = null
    );

    public sealed record GenerateModuleFromActivityRequestBody(
        Guid ActivityDefinitionId, string? Title = null, string? Notes = null
    );

    public sealed record UpdateModuleDefinitionRequestBody(
        string Title, string? Description = null, string? CefrLevel = null, string? Skill = null, string? Subskill = null,
        IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null, string? FeedbackPlanJson = null
    );

    public sealed record ApproveModuleDefinitionRequestBody(string? Notes = null);
    public sealed record RejectModuleDefinitionRequestBody(string Reason);
}
