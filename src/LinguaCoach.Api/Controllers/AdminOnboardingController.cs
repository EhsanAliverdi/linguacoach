using System.Security.Claims;
using LinguaCoach.Application.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/onboarding")]
[Authorize(Roles = "Admin")]
public sealed class AdminOnboardingController : ControllerBase
{
    private readonly IAdminListOnboardingTemplatesQuery _listQuery;
    private readonly IAdminGetOnboardingTemplateQuery _getQuery;
    private readonly IAdminGetActiveOnboardingTemplateQuery _getActiveQuery;
    private readonly IAdminCreateOnboardingTemplateHandler _createHandler;
    private readonly IAdminSaveOnboardingTemplateDraftHandler _saveDraftHandler;
    private readonly IAdminPublishOnboardingTemplateHandler _publishHandler;
    private readonly IAdminArchiveOnboardingTemplateHandler _archiveHandler;

    public AdminOnboardingController(
        IAdminListOnboardingTemplatesQuery listQuery,
        IAdminGetOnboardingTemplateQuery getQuery,
        IAdminGetActiveOnboardingTemplateQuery getActiveQuery,
        IAdminCreateOnboardingTemplateHandler createHandler,
        IAdminSaveOnboardingTemplateDraftHandler saveDraftHandler,
        IAdminPublishOnboardingTemplateHandler publishHandler,
        IAdminArchiveOnboardingTemplateHandler archiveHandler)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _getActiveQuery = getActiveQuery;
        _createHandler = createHandler;
        _saveDraftHandler = saveDraftHandler;
        _publishHandler = publishHandler;
        _archiveHandler = archiveHandler;
    }

    // GET api/admin/onboarding/templates
    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates(CancellationToken ct)
    {
        var result = await _listQuery.HandleAsync(new ListOnboardingTemplatesQuery(), ct);
        return Ok(result);
    }

    // GET api/admin/onboarding/templates/active
    [HttpGet("templates/active")]
    public async Task<IActionResult> GetActiveTemplate(CancellationToken ct)
    {
        var result = await _getActiveQuery.HandleAsync(new GetActiveOnboardingTemplateQuery(), ct);
        if (result is null) return NotFound(new { error = "No published onboarding template found." });
        return Ok(result);
    }

    // GET api/admin/onboarding/templates/{templateId}
    [HttpGet("templates/{templateId:guid}")]
    public async Task<IActionResult> GetTemplate(Guid templateId, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetOnboardingTemplateQuery(templateId), ct);
        if (result is null) return NotFound(new { error = $"Template {templateId} not found." });
        return Ok(result);
    }

    // POST api/admin/onboarding/templates
    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        try
        {
            var result = await _createHandler.HandleAsync(
                new CreateOnboardingTemplateCommand(request.Name, request.Description, adminId), ct);
            return CreatedAtAction(nameof(GetTemplate), new { templateId = result.TemplateId }, result);
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/onboarding/templates/{templateId}/draft
    [HttpPut("templates/{templateId:guid}/draft")]
    public async Task<IActionResult> SaveDraft(Guid templateId, [FromBody] SaveDraftRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        try
        {
            var result = await _saveDraftHandler.HandleAsync(
                new SaveOnboardingTemplateDraftCommand(templateId, request.FormIoSchemaJson, request.ScoringRulesJson, adminId, request.RendererKind ?? "FormIo"), ct);
            return Ok(result);
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/onboarding/templates/{templateId}/publish
    [HttpPost("templates/{templateId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid templateId, CancellationToken ct)
    {
        try
        {
            var result = await _publishHandler.HandleAsync(new PublishOnboardingTemplateCommand(templateId), ct);
            return Ok(result);
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/onboarding/templates/{templateId}/archive
    [HttpPost("templates/{templateId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid templateId, CancellationToken ct)
    {
        try
        {
            await _archiveHandler.HandleAsync(new ArchiveOnboardingTemplateCommand(templateId), ct);
            return NoContent();
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    public sealed record CreateTemplateRequest(string Name, string? Description);
    public sealed record SaveDraftRequest(string FormIoSchemaJson, string? ScoringRulesJson, string? RendererKind = null);
}
