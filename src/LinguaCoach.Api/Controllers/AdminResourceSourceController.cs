using LinguaCoach.Application.ResourceImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase E1 — CefrResourceSource admin CRUD (the reused English resource source registry).
/// Import-approval stays governed only by the explicit approve/revoke actions, never by the
/// generic metadata update.
/// </summary>
[ApiController]
[Route("api/admin/resource-sources")]
[Authorize(Roles = "Admin")]
public sealed class AdminResourceSourceController : ControllerBase
{
    private readonly IAdminResourceSourceListQuery _listQuery;
    private readonly IAdminResourceSourceGetQuery _getQuery;
    private readonly IAdminAddResourceSourceHandler _addSource;
    private readonly IAdminUpdateResourceSourceHandler _updateSource;
    private readonly IAdminResourceSourceApprovalHandler _approvalHandler;

    public AdminResourceSourceController(
        IAdminResourceSourceListQuery listQuery,
        IAdminResourceSourceGetQuery getQuery,
        IAdminAddResourceSourceHandler addSource,
        IAdminUpdateResourceSourceHandler updateSource,
        IAdminResourceSourceApprovalHandler approvalHandler)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _addSource = addSource;
        _updateSource = updateSource;
        _approvalHandler = approvalHandler;
    }

    // GET api/admin/resource-sources?page=1&pageSize=20&isImportApproved=true&languageCode=en&search=cefr
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isImportApproved = null,
        [FromQuery] string? languageCode = null, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(
            new ListAdminResourceSourcesQuery(page, pageSize, isImportApproved, languageCode, search), ct);
        return Ok(result);
    }

    // GET api/admin/resource-sources/{sourceId}
    [HttpGet("{sourceId:guid}")]
    public async Task<IActionResult> Get(Guid sourceId, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetAdminResourceSourceQuery(sourceId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/resource-sources
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] ResourceSourceRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _addSource.HandleAsync(new AddResourceSourceCommand(
                request.Name, request.LicenseType, request.SourceUrl, request.UsageRestrictionNotes,
                request.LanguageCode, request.AllowsStudentDisplay, request.AllowsCommercialUse,
                request.AttributionText, request.SourceVersion, request.DownloadUrl), ct);
            return Ok(result);
        }
        catch (ResourceSourceValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/resource-sources/{sourceId}
    [HttpPut("{sourceId:guid}")]
    public async Task<IActionResult> Update(Guid sourceId, [FromBody] ResourceSourceRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _updateSource.HandleAsync(new UpdateResourceSourceCommand(
                sourceId, request.Name, request.LicenseType, request.SourceUrl, request.UsageRestrictionNotes,
                request.LanguageCode, request.AllowsStudentDisplay, request.AllowsCommercialUse,
                request.AttributionText, request.SourceVersion, request.DownloadUrl), ct);
            return Ok(result);
        }
        catch (ResourceSourceValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/resource-sources/{sourceId}/approve  { reason? }
    [HttpPost("{sourceId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid sourceId, [FromBody] ResourceSourceApprovalRequest? request, CancellationToken ct)
    {
        try
        {
            var result = await _approvalHandler.HandleAsync(
                new SetResourceSourceApprovalCommand(sourceId, true, request?.Reason), ct);
            return Ok(result);
        }
        catch (ResourceSourceValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/resource-sources/{sourceId}/revoke  { reason }
    [HttpPost("{sourceId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid sourceId, [FromBody] ResourceSourceApprovalRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _approvalHandler.HandleAsync(
                new SetResourceSourceApprovalCommand(sourceId, false, request.Reason), ct);
            return Ok(result);
        }
        catch (ResourceSourceValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Request models ────────────────────────────────────────────────────────

    public sealed record ResourceSourceRequest(
        string Name,
        string LicenseType,
        string? SourceUrl,
        string? UsageRestrictionNotes,
        string LanguageCode,
        bool AllowsStudentDisplay,
        bool AllowsCommercialUse,
        string? AttributionText,
        string? SourceVersion,
        string? DownloadUrl);

    public sealed record ResourceSourceApprovalRequest(string? Reason = null);
}
