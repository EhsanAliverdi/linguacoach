using System.Security.Claims;
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
    private readonly IResourceBankArchiveHandler _archiveHandler;
    private readonly IQuickWordPipelineService _quickWordPipeline;

    public AdminResourceBankController(
        IResourceBankQueryService bankQueryService, IResourceBankArchiveHandler archiveHandler,
        IQuickWordPipelineService quickWordPipeline)
    {
        _bankQueryService = bankQueryService;
        _archiveHandler = archiveHandler;
        _quickWordPipeline = quickWordPipeline;
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

    // GET api/admin/resource-bank/{id}
    // Phase K3 — single-row lookup backing the admin "view as its own page" detail route.
    [HttpGet("api/admin/resource-bank/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _bankQueryService.GetUnifiedByIdAsync(id, ct);
        return result is null ? NotFound(new { error = $"Resource Bank item '{id}' was not found." }) : Ok(result);
    }

    // POST api/admin/resource-bank/archive  { ids: [...] }
    // Phase K3 — soft-delete: hides the row(s) from the default list/browse view without breaking
    // any Lesson/Exercise/Module that already links to them.
    [HttpPost("api/admin/resource-bank/archive")]
    public async Task<IActionResult> Archive([FromBody] ResourceBankIdsRequest request, CancellationToken ct)
    {
        var result = await _archiveHandler.ArchiveAsync(new ArchiveResourceBankItemsCommand(request.Ids), ct);
        return Ok(result);
    }

    // POST api/admin/resource-bank/unarchive  { ids: [...] }
    [HttpPost("api/admin/resource-bank/unarchive")]
    public async Task<IActionResult> Unarchive([FromBody] ResourceBankIdsRequest request, CancellationToken ct)
    {
        var result = await _archiveHandler.UnarchiveAsync(new UnarchiveResourceBankItemsCommand(request.Ids), ct);
        return Ok(result);
    }

    // POST api/admin/resource-bank/quick-word  { word, cefrLevel, partOfSpeech?, definition? }
    // Phase K3 — one-click cascade: publishes a Vocabulary Resource Bank item then generates and
    // auto-approves a Lesson and Exercise, then generates a Module from them. See
    // IQuickWordPipelineService's doc comment — this bypasses the normal import/review workflow,
    // it's an admin dev/testing shortcut, not a replacement for it.
    [HttpPost("api/admin/resource-bank/quick-word")]
    public async Task<IActionResult> QuickWord([FromBody] QuickWordRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _quickWordPipeline.RunAsync(new QuickWordPipelineRequest(
                request.Word, request.CefrLevel, request.PartOfSpeech, request.Definition, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public sealed record ResourceBankIdsRequest(IReadOnlyList<Guid> Ids);
    public sealed record QuickWordRequest(string Word, string CefrLevel, string? PartOfSpeech = null, string? Definition = null);
}
