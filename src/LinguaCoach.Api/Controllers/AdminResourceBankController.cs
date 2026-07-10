using LinguaCoach.Application.ResourceImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase I0 — read-only browse/search for the unified Resource Bank
/// (<see cref="IResourceBankQueryService.ListUnifiedAsync"/>, backed by the single consolidated
/// ResourceBankItem table). The four typed HTTP routes this controller used to expose
/// (vocabulary/grammar/reading-references/reading-passages) were removed in this phase — their
/// only caller was the admin frontend's typed bank pages, already deleted in Phase H9A. Mutation
/// (approve/reject/publish) still lives solely in <see cref="AdminResourceCandidateController"/>.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
public sealed class AdminResourceBankController : ControllerBase
{
    private readonly IResourceBankQueryService _bankQueryService;

    public AdminResourceBankController(IResourceBankQueryService bankQueryService)
    {
        _bankQueryService = bankQueryService;
    }

    // GET api/admin/resource-bank?type=&cefrLevel=&skill=&subskill=&contextTag=&focusTag=
    //     &difficultyBand=&search=&sourceId=&page=1&pageSize=20
    [HttpGet("api/admin/resource-bank")]
    public async Task<IActionResult> ListUnified(
        [FromQuery] string? type = null, [FromQuery] string? cefrLevel = null, [FromQuery] string? skill = null,
        [FromQuery] string? subskill = null, [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] int? difficultyBand = null, [FromQuery] string? search = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        UnifiedResourceBankItemType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<UnifiedResourceBankItemType>(type, ignoreCase: true, out var value))
                return BadRequest(new { error = $"Unknown resource bank type '{type}'." });
            parsedType = value;
        }

        var result = await _bankQueryService.ListUnifiedAsync(
            new UnifiedResourceBankListFilter(
                parsedType, cefrLevel, skill, subskill, contextTag, focusTag, difficultyBand, search, sourceId, page, pageSize),
            ct);
        return Ok(result);
    }
}
