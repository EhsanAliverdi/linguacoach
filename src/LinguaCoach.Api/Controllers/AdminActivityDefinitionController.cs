using System.Security.Claims;
using LinguaCoach.Application.ActivityDefinitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H4 — Activity foundation. Reviewable, editable practice task designs generated from (or
/// manually authored about) selected published Resource Bank rows, optionally linked to a Learn
/// Item — the "Practice" half of a future Module. Distinct from the existing runtime
/// <c>LearningActivity</c> (per-student delivery record) and <c>ActivityTemplate</c> (already
/// wired into the live Practice Gym Form.io pilot) — see <c>ActivityDefinition</c>'s doc comment.
/// Every create/generate action stages a pending-review row; only <see cref="Approve"/>/
/// <see cref="Reject"/> change that. Never creates a Module row, never assigns anything to a
/// student, never changes Today/Practice Gym runtime selection.
/// </summary>
[ApiController]
[Route("api/admin/activities")]
[Authorize(Roles = "Admin")]
public sealed class AdminActivityDefinitionController : ControllerBase
{
    private readonly IAdminActivityDefinitionListQuery _listQuery;
    private readonly IAdminActivityDefinitionGetQuery _getQuery;
    private readonly IAdminCreateActivityDefinitionHandler _createHandler;
    private readonly IAdminUpdateActivityDefinitionHandler _updateHandler;
    private readonly IAdminApproveActivityDefinitionHandler _approveHandler;
    private readonly IAdminRejectActivityDefinitionHandler _rejectHandler;
    private readonly IGenerateActivityFromResourcesHandler _generateFromResourcesHandler;
    private readonly IGenerateActivityFromLearnItemHandler _generateFromLearnItemHandler;

    public AdminActivityDefinitionController(
        IAdminActivityDefinitionListQuery listQuery,
        IAdminActivityDefinitionGetQuery getQuery,
        IAdminCreateActivityDefinitionHandler createHandler,
        IAdminUpdateActivityDefinitionHandler updateHandler,
        IAdminApproveActivityDefinitionHandler approveHandler,
        IAdminRejectActivityDefinitionHandler rejectHandler,
        IGenerateActivityFromResourcesHandler generateFromResourcesHandler,
        IGenerateActivityFromLearnItemHandler generateFromLearnItemHandler)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _generateFromResourcesHandler = generateFromResourcesHandler;
        _generateFromLearnItemHandler = generateFromLearnItemHandler;
    }

    // GET api/admin/activities?page=&pageSize=&status=&activityType=&rendererType=&cefrLevel=&
    //   skill=&subskill=&contextTag=&focusTag=&difficultyBand=&learnItemId=&search=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null,
        [FromQuery] string? activityType = null, [FromQuery] string? rendererType = null,
        [FromQuery] string? cefrLevel = null, [FromQuery] string? skill = null, [FromQuery] string? subskill = null,
        [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] int? difficultyBand = null, [FromQuery] Guid? learnItemId = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(new ListActivityDefinitionsQuery(
            page, pageSize, status, activityType, rendererType, cefrLevel, skill, subskill,
            contextTag, focusTag, difficultyBand, learnItemId, search), ct);
        return Ok(result);
    }

    // GET api/admin/activities/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetActivityDefinitionQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/activities
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateActivityDefinitionRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _createHandler.HandleAsync(new CreateActivityDefinitionCommand(
                body.Title, body.Instructions, body.ActivityType, body.RendererType, body.Description, body.PatternKey,
                body.FormSchemaJson, body.AnswerKeyJson, body.ScoringRulesJson, body.FeedbackPlanJson,
                body.CefrLevel, body.Skill, body.Subskill, body.ContextTags, body.FocusTags,
                body.DifficultyBand, body.EstimatedMinutes, body.LearnItemId, body.Links, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ActivityDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/activities/generate-from-resources
    [HttpPost("generate-from-resources")]
    public async Task<IActionResult> GenerateFromResources(
        [FromBody] GenerateActivityFromResourcesRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromResourcesHandler.HandleAsync(new GenerateActivityFromResourcesRequest(
                body.Resources, body.RequestedActivityType, body.Title, body.DefaultCefrLevel, body.DefaultSkill,
                body.DefaultSubskill, body.DefaultContextTags, body.DefaultFocusTags, body.DefaultDifficultyBand,
                body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ActivityDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/activities/generate-from-learn-item
    [HttpPost("generate-from-learn-item")]
    public async Task<IActionResult> GenerateFromLearnItem(
        [FromBody] GenerateActivityFromLearnItemRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromLearnItemHandler.HandleAsync(new GenerateActivityFromLearnItemRequest(
                body.LearnItemId, body.RequestedActivityType, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ActivityDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/activities/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateActivityDefinitionRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _updateHandler.HandleAsync(new UpdateActivityDefinitionCommand(
                id, body.Title, body.Instructions, body.Description, body.FormSchemaJson, body.AnswerKeyJson,
                body.ScoringRulesJson, body.FeedbackPlanJson, body.CefrLevel, body.Skill, body.Subskill,
                body.ContextTags, body.FocusTags, body.DifficultyBand, body.EstimatedMinutes), ct);
            return Ok(result);
        }
        catch (ActivityDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/activities/{id}/approve  { notes? }
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveActivityDefinitionRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _approveHandler.HandleAsync(new ApproveActivityDefinitionCommand(id, GetCurrentUserId(), body.Notes), ct);
            return Ok(result);
        }
        catch (ActivityDefinitionValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/activities/{id}/reject  { reason }
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectActivityDefinitionRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _rejectHandler.HandleAsync(new RejectActivityDefinitionCommand(id, body.Reason, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ActivityDefinitionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public sealed record CreateActivityDefinitionRequestBody(
        string Title, string Instructions, string ActivityType, string RendererType,
        string? Description = null, string? PatternKey = null, string? FormSchemaJson = null,
        string? AnswerKeyJson = null, string? ScoringRulesJson = null, string? FeedbackPlanJson = null,
        string? CefrLevel = null, string? Skill = null, string? Subskill = null,
        IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null, Guid? LearnItemId = null,
        IReadOnlyList<ActivityResourceLinkInput>? Links = null
    );

    public sealed record GenerateActivityFromResourcesRequestBody(
        IReadOnlyList<ActivityResourceLinkInput> Resources,
        string? RequestedActivityType = null, string? Title = null, string? DefaultCefrLevel = null,
        string? DefaultSkill = null, string? DefaultSubskill = null,
        IReadOnlyList<string>? DefaultContextTags = null, IReadOnlyList<string>? DefaultFocusTags = null,
        int? DefaultDifficultyBand = null, string? Notes = null
    );

    public sealed record GenerateActivityFromLearnItemRequestBody(
        Guid LearnItemId, string? RequestedActivityType = null, string? Title = null, string? Notes = null
    );

    public sealed record UpdateActivityDefinitionRequestBody(
        string Title, string Instructions, string? Description = null, string? FormSchemaJson = null,
        string? AnswerKeyJson = null, string? ScoringRulesJson = null, string? FeedbackPlanJson = null,
        string? CefrLevel = null, string? Skill = null, string? Subskill = null,
        IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null
    );

    public sealed record ApproveActivityDefinitionRequestBody(string? Notes = null);
    public sealed record RejectActivityDefinitionRequestBody(string Reason);
}
