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
    private readonly IResourceBankItemUpdateHandler _updateHandler;
    private readonly IResourceBankRepairService _repairService;
    private readonly IResourceBankMediaService _mediaService;

    public AdminResourceBankController(
        IResourceBankQueryService bankQueryService, IResourceBankArchiveHandler archiveHandler,
        IResourceBankItemUpdateHandler updateHandler, IResourceBankRepairService repairService,
        IResourceBankMediaService mediaService)
    {
        _bankQueryService = bankQueryService;
        _archiveHandler = archiveHandler;
        _updateHandler = updateHandler;
        _repairService = repairService;
        _mediaService = mediaService;
    }

    // GET api/admin/resource-bank/{id}/audio-url
    // Phase 4.6 — short-lived signed URL (or, for local storage, the authenticated streaming
    // endpoint below) for a published Listening item's audio. Mirrors the candidate audio-url
    // route exactly. 404 for anything that isn't a Listening item with audio recorded.
    [HttpGet("api/admin/resource-bank/{id:guid}/audio-url")]
    public async Task<IActionResult> GetAudioUrl(Guid id, CancellationToken ct)
    {
        var result = await _mediaService.GetAudioUrlAsync(id, ct);
        return result is null ? NotFound(new { error = "No audio is available for this Resource Bank item." }) : Ok(result);
    }

    // GET api/admin/resource-bank/{id}/audio
    // Phase 4.6 — raw audio stream, the local-storage fallback for GetAudioUrl's signed URL.
    [HttpGet("api/admin/resource-bank/{id:guid}/audio")]
    public async Task<IActionResult> GetAudio(Guid id, CancellationToken ct)
    {
        var result = await _mediaService.GetAudioStreamAsync(id, ct);
        return result is null ? NotFound() : File(result.Bytes, result.ContentType);
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

    // GET api/admin/resource-bank/{id}/edit
    // Phase K5 — the full, untruncated, type-specific field set for the edit form (the list/detail
    // DTO has a lossy, sometimes-truncated Title/Summary — not enough to safely round-trip edits).
    [HttpGet("api/admin/resource-bank/{id:guid}/edit")]
    public async Task<IActionResult> GetEditDto(Guid id, CancellationToken ct)
    {
        var result = await _bankQueryService.GetEditDtoAsync(id, ct);
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

    // PUT api/admin/resource-bank/{id}
    // Phase K5 — admin edit of a published item's content/metadata. Full-replace per item Type;
    // the admin UI is expected to have loaded the current value first (same PUT convention as
    // Lesson/Exercise/Module).
    [HttpPut("api/admin/resource-bank/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateResourceBankItemRequest body, CancellationToken ct)
    {
        try
        {
            var result = await _updateHandler.HandleAsync(new UpdateResourceBankItemCommand(
                id, body.CefrLevel, body.Subskill, body.DifficultyBand, body.ContextTags, body.FocusTags,
                body.Word, body.PartOfSpeech, body.Notes, body.GrammarPoint, body.Description,
                body.TextType, body.DifficultyNotes, body.ReferenceExcerpt, body.Title, body.PassageText,
                body.Summary, body.PromptText, body.Genre, body.SuggestedMinWords, body.Transcript,
                body.SuggestedDurationSeconds, body.ImageUrl), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/resource-bank/{id}/diagnostics
    // Phase K8 — issues found on this item (e.g. a Vocabulary item missing its definition).
    [HttpGet("api/admin/resource-bank/{id:guid}/diagnostics")]
    public async Task<IActionResult> Diagnose(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _repairService.DiagnoseAsync(id, ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/resource-bank/{id}/repair
    // Phase K8 — AI-fills the missing core field(s) found by Diagnose above.
    [HttpPost("api/admin/resource-bank/{id:guid}/repair")]
    public async Task<IActionResult> Repair(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _repairService.RepairAsync(id, ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/resource-bank/issues-summary
    // Phase K9 — how many non-archived items have at least one auto-fixable issue.
    [HttpGet("api/admin/resource-bank/issues-summary")]
    public async Task<IActionResult> IssuesSummary(CancellationToken ct)
    {
        var result = await _repairService.GetIssuesSummaryAsync(ct);
        return Ok(result);
    }

    // GET api/admin/resource-bank/with-issues
    // Phase K10 — id + title of every item with an auto-fixable issue, for the frontend to drive
    // a client-side "Fix All with AI" progress loop over the single-item repair endpoint.
    [HttpGet("api/admin/resource-bank/with-issues")]
    public async Task<IActionResult> ListWithIssues(CancellationToken ct)
    {
        var result = await _repairService.ListWithIssuesAsync(ct);
        return Ok(result);
    }

    // POST api/admin/resource-bank/repair-all
    // Phase K9 — AI-repairs every non-archived item with an auto-fixable issue.
    [HttpPost("api/admin/resource-bank/repair-all")]
    public async Task<IActionResult> RepairAll(CancellationToken ct)
    {
        var result = await _repairService.RepairAllAsync(ct);
        return Ok(result);
    }

    public sealed record ResourceBankIdsRequest(IReadOnlyList<Guid> Ids);

    public sealed record UpdateResourceBankItemRequest(
        string CefrLevel,
        string? Subskill = null,
        int? DifficultyBand = null,
        IReadOnlyList<string>? ContextTags = null,
        IReadOnlyList<string>? FocusTags = null,
        string? Word = null,
        string? PartOfSpeech = null,
        string? Notes = null,
        string? GrammarPoint = null,
        string? Description = null,
        string? TextType = null,
        string? DifficultyNotes = null,
        string? ReferenceExcerpt = null,
        string? Title = null,
        string? PassageText = null,
        string? Summary = null,
        string? PromptText = null,
        string? Genre = null,
        int? SuggestedMinWords = null,
        string? Transcript = null,
        int? SuggestedDurationSeconds = null,
        string? ImageUrl = null
    );
}
