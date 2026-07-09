using LinguaCoach.Application.ResourceImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase E5 — read-only browse/search/admin management for the published Cefr* bank tables
/// (Vocabulary/Grammar/ReadingReference). Browse/search only: no edit or delete actions exist
/// here — all mutation (approve/reject/publish) still lives solely in
/// <see cref="AdminResourceCandidateController"/>'s Phase E4 workflow.
/// </summary>
[ApiController]
[Route("api/admin/resource-banks")]
[Authorize(Roles = "Admin")]
public sealed class AdminResourceBankController : ControllerBase
{
    private readonly IResourceBankQueryService _bankQueryService;

    public AdminResourceBankController(IResourceBankQueryService bankQueryService)
    {
        _bankQueryService = bankQueryService;
    }

    // GET api/admin/resource-banks/vocabulary?search=&cefrLevel=&sourceId=&page=1&pageSize=20
    //     &contextTag=&focusTag=&subskill=&difficultyBand=   (Phase E9 metadata filters)
    [HttpGet("vocabulary")]
    public async Task<IActionResult> ListVocabulary(
        [FromQuery] string? search = null, [FromQuery] string? cefrLevel = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] string? subskill = null, [FromQuery] int? difficultyBand = null, CancellationToken ct = default)
    {
        var result = await _bankQueryService.ListVocabularyAsync(
            new ResourceBankListFilter(search, cefrLevel, sourceId, page, pageSize,
                contextTag, focusTag, subskill, difficultyBand), ct);
        return Ok(result);
    }

    // GET api/admin/resource-banks/vocabulary/{id}
    [HttpGet("vocabulary/{id:guid}")]
    public async Task<IActionResult> GetVocabularyDetail(Guid id, CancellationToken ct)
    {
        var result = await _bankQueryService.GetVocabularyDetailAsync(id, ct);
        return result is null ? NotFound(new { error = $"Vocabulary bank entry '{id}' was not found." }) : Ok(result);
    }

    // GET api/admin/resource-banks/grammar?search=&cefrLevel=&sourceId=&page=1&pageSize=20
    //     &contextTag=&focusTag=&subskill=&difficultyBand=   (Phase E9 metadata filters)
    [HttpGet("grammar")]
    public async Task<IActionResult> ListGrammar(
        [FromQuery] string? search = null, [FromQuery] string? cefrLevel = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] string? subskill = null, [FromQuery] int? difficultyBand = null, CancellationToken ct = default)
    {
        var result = await _bankQueryService.ListGrammarAsync(
            new ResourceBankListFilter(search, cefrLevel, sourceId, page, pageSize,
                contextTag, focusTag, subskill, difficultyBand), ct);
        return Ok(result);
    }

    // GET api/admin/resource-banks/grammar/{id}
    [HttpGet("grammar/{id:guid}")]
    public async Task<IActionResult> GetGrammarDetail(Guid id, CancellationToken ct)
    {
        var result = await _bankQueryService.GetGrammarDetailAsync(id, ct);
        return result is null ? NotFound(new { error = $"Grammar bank entry '{id}' was not found." }) : Ok(result);
    }

    // GET api/admin/resource-banks/reading-references?search=&cefrLevel=&sourceId=&page=1&pageSize=20
    //     &contextTag=&focusTag=&subskill=&difficultyBand=   (Phase E9 metadata filters)
    [HttpGet("reading-references")]
    public async Task<IActionResult> ListReadingReferences(
        [FromQuery] string? search = null, [FromQuery] string? cefrLevel = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] string? subskill = null, [FromQuery] int? difficultyBand = null, CancellationToken ct = default)
    {
        var result = await _bankQueryService.ListReadingReferencesAsync(
            new ResourceBankListFilter(search, cefrLevel, sourceId, page, pageSize,
                contextTag, focusTag, subskill, difficultyBand), ct);
        return Ok(result);
    }

    // GET api/admin/resource-banks/reading-references/{id}
    [HttpGet("reading-references/{id:guid}")]
    public async Task<IActionResult> GetReadingReferenceDetail(Guid id, CancellationToken ct)
    {
        var result = await _bankQueryService.GetReadingReferenceDetailAsync(id, ct);
        return result is null ? NotFound(new { error = $"Reading reference bank entry '{id}' was not found." }) : Ok(result);
    }

    // GET api/admin/resource-banks/reading-passages?search=&cefrLevel=&sourceId=&page=1&pageSize=20
    [HttpGet("reading-passages")]
    public async Task<IActionResult> ListReadingPassages(
        [FromQuery] string? search = null, [FromQuery] string? cefrLevel = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _bankQueryService.ListReadingPassagesAsync(
            new ResourceBankListFilter(search, cefrLevel, sourceId, page, pageSize), ct);
        return Ok(result);
    }

    // GET api/admin/resource-banks/reading-passages/{id}
    [HttpGet("reading-passages/{id:guid}")]
    public async Task<IActionResult> GetReadingPassageDetail(Guid id, CancellationToken ct)
    {
        var result = await _bankQueryService.GetReadingPassageDetailAsync(id, ct);
        return result is null ? NotFound(new { error = $"Reading passage bank entry '{id}' was not found." }) : Ok(result);
    }

    // Phase H1 — unified Resource Bank read model. Route override (~/) puts this at the singular
    // "api/admin/resource-bank", distinct from this controller's plural "api/admin/resource-banks"
    // base route used by the four typed endpoints above. Read-only, same as everything else in
    // this controller — aggregates the same four typed tables, does not add a new table.
    //
    // GET api/admin/resource-bank?type=&cefrLevel=&skill=&subskill=&contextTag=&focusTag=
    //     &difficultyBand=&search=&sourceId=&page=1&pageSize=20
    [HttpGet("~/api/admin/resource-bank")]
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
