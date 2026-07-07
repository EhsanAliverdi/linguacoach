using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/activity-templates")]
[Authorize(Roles = "Admin")]
public sealed class AdminActivityTemplateController : ControllerBase
{
    private readonly IAdminActivityTemplateListQuery _listQuery;
    private readonly IAdminActivityTemplateGetQuery _getQuery;
    private readonly IAdminAddActivityTemplateHandler _addTemplate;
    private readonly IAdminUpdateActivityTemplateHandler _updateTemplate;
    private readonly IAdminRemoveActivityTemplateHandler _removeTemplate;
    private readonly IAdminActivityTemplateReviewHandler _reviewTemplate;
    private readonly IAdminActivityTemplatePublishHandler _publishTemplate;
    private readonly IActivityTemplateInstanceGenerator _instanceGenerator;

    public AdminActivityTemplateController(
        IAdminActivityTemplateListQuery listQuery,
        IAdminActivityTemplateGetQuery getQuery,
        IAdminAddActivityTemplateHandler addTemplate,
        IAdminUpdateActivityTemplateHandler updateTemplate,
        IAdminRemoveActivityTemplateHandler removeTemplate,
        IAdminActivityTemplateReviewHandler reviewTemplate,
        IAdminActivityTemplatePublishHandler publishTemplate,
        IActivityTemplateInstanceGenerator instanceGenerator)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _addTemplate = addTemplate;
        _updateTemplate = updateTemplate;
        _removeTemplate = removeTemplate;
        _reviewTemplate = reviewTemplate;
        _publishTemplate = publishTemplate;
        _instanceGenerator = instanceGenerator;
    }

    // GET api/admin/activity-templates?page=1&pageSize=20&skill=speaking&cefrLevel=B1&reviewStatus=Approved&search=roleplay
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? skill = null,
        [FromQuery] string? cefrLevel = null, [FromQuery] string? reviewStatus = null,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(
            new ListAdminActivityTemplatesQuery(page, pageSize, skill, cefrLevel, reviewStatus, search), ct);
        return Ok(result);
    }

    // GET api/admin/activity-templates/{templateId}
    [HttpGet("{templateId:guid}")]
    public async Task<IActionResult> Get(Guid templateId, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetAdminActivityTemplateQuery(templateId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/activity-templates
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] ActivityTemplateCreateRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _addTemplate.HandleAsync(new AddActivityTemplateCommand(
                request.Key, request.Skill, request.CefrLevel, request.ActivityType,
                request.Subskill, request.PatternKey, request.ContextTagsJson ?? "[]", request.FocusTagsJson ?? "[]",
                request.CurriculumObjectiveKey, request.FormIoBaseSchemaJson, request.GenerationInstructions,
                request.ScoringModelJson, request.ValidationRulesJson,
                request.EstimatedDurationSeconds, request.AssetRequirementsJson), ct);
            return Ok(result);
        }
        catch (ActivityTemplateValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/activity-templates/{templateId}
    [HttpPut("{templateId:guid}")]
    public async Task<IActionResult> Update(Guid templateId, [FromBody] ActivityTemplateUpdateRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _updateTemplate.HandleAsync(new UpdateActivityTemplateCommand(
                templateId, request.Skill, request.CefrLevel, request.ActivityType,
                request.Subskill, request.PatternKey, request.ContextTagsJson ?? "[]", request.FocusTagsJson ?? "[]",
                request.CurriculumObjectiveKey, request.FormIoBaseSchemaJson, request.GenerationInstructions,
                request.ScoringModelJson, request.ValidationRulesJson,
                request.EstimatedDurationSeconds, request.AssetRequirementsJson), ct);
            return Ok(result);
        }
        catch (ActivityTemplateValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // DELETE api/admin/activity-templates/{templateId}
    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> Remove(Guid templateId, CancellationToken ct)
    {
        try
        {
            await _removeTemplate.HandleAsync(new RemoveActivityTemplateCommand(templateId), ct);
            return NoContent();
        }
        catch (ActivityTemplateValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/activity-templates/{templateId}/review  { action: "approve"|"reject"|"reset", reason? }
    [HttpPost("{templateId:guid}/review")]
    public async Task<IActionResult> SetReviewStatus(Guid templateId, [FromBody] ActivityTemplateReviewRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _reviewTemplate.HandleAsync(
                new SetActivityTemplateReviewStatusCommand(templateId, request.Action, request.Reason), ct);
            return Ok(result);
        }
        catch (ActivityTemplateValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/activity-templates/{templateId}/publish  { publish: true|false }
    [HttpPost("{templateId:guid}/publish")]
    public async Task<IActionResult> SetPublished(Guid templateId, [FromBody] ActivityTemplatePublishRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _publishTemplate.HandleAsync(
                new SetActivityTemplatePublishedCommand(templateId, request.Publish), ct);
            return Ok(result);
        }
        catch (ActivityTemplateValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/activity-templates/{templateId}/generate-preview
    // Phase 5 — AI Bank-First Teaching Architecture: proves the AI generation + validation
    // pipeline against an ActivityTemplate. Never persists anything.
    [HttpPost("{templateId:guid}/generate-preview")]
    public async Task<IActionResult> GeneratePreview(
        Guid templateId, [FromBody] ActivityTemplateGeneratePreviewRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _instanceGenerator.GenerateInstanceAsync(
                templateId,
                new ActivityTemplateInstanceGenerationContext(
                    CefrLevelOverride: request.CefrLevelOverride,
                    TopicHint: request.TopicHint),
                ct);
            return Ok(result);
        }
        catch (ActivityTemplateValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (AiResponseValidationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (AiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    // ── Request models ────────────────────────────────────────────────────────

    public sealed record ActivityTemplateCreateRequest(
        string Key,
        string Skill,
        string CefrLevel,
        string ActivityType,
        string? Subskill = null,
        string? PatternKey = null,
        string? ContextTagsJson = null,
        string? FocusTagsJson = null,
        string? CurriculumObjectiveKey = null,
        string? FormIoBaseSchemaJson = null,
        string? GenerationInstructions = null,
        string? ScoringModelJson = null,
        string? ValidationRulesJson = null,
        int? EstimatedDurationSeconds = null,
        string? AssetRequirementsJson = null);

    public sealed record ActivityTemplateUpdateRequest(
        string Skill,
        string CefrLevel,
        string ActivityType,
        string? Subskill = null,
        string? PatternKey = null,
        string? ContextTagsJson = null,
        string? FocusTagsJson = null,
        string? CurriculumObjectiveKey = null,
        string? FormIoBaseSchemaJson = null,
        string? GenerationInstructions = null,
        string? ScoringModelJson = null,
        string? ValidationRulesJson = null,
        int? EstimatedDurationSeconds = null,
        string? AssetRequirementsJson = null);

    public sealed record ActivityTemplateReviewRequest(string Action, string? Reason = null);

    public sealed record ActivityTemplatePublishRequest(bool Publish);

    public sealed record ActivityTemplateGeneratePreviewRequest(string? CefrLevelOverride = null, string? TopicHint = null);
}
