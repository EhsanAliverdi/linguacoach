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
    [HttpGet("vocabulary")]
    public async Task<IActionResult> ListVocabulary(
        [FromQuery] string? search = null, [FromQuery] string? cefrLevel = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _bankQueryService.ListVocabularyAsync(
            new ResourceBankListFilter(search, cefrLevel, sourceId, page, pageSize), ct);
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
    [HttpGet("grammar")]
    public async Task<IActionResult> ListGrammar(
        [FromQuery] string? search = null, [FromQuery] string? cefrLevel = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _bankQueryService.ListGrammarAsync(
            new ResourceBankListFilter(search, cefrLevel, sourceId, page, pageSize), ct);
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
    [HttpGet("reading-references")]
    public async Task<IActionResult> ListReadingReferences(
        [FromQuery] string? search = null, [FromQuery] string? cefrLevel = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _bankQueryService.ListReadingReferencesAsync(
            new ResourceBankListFilter(search, cefrLevel, sourceId, page, pageSize), ct);
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
}
