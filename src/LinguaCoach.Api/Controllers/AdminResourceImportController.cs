using System.Security.Claims;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase E1 — resource import runs, raw records, and staged candidates. Read/upload only: no
/// approve/publish/reject-with-workflow actions here (Phase E4 publishes to the Cefr* bank,
/// which this controller never touches). Candidates support only an AdminNotes edit.
/// </summary>
[ApiController]
[Route("api/admin/resource-import-runs")]
[Authorize(Roles = "Admin")]
public sealed class AdminResourceImportController : ControllerBase
{
    private readonly IAdminResourceImportRunListQuery _runListQuery;
    private readonly IAdminResourceImportRunGetQuery _runGetQuery;
    private readonly IAdminResourceRawRecordListQuery _rawRecordListQuery;
    private readonly IAdminResourceRawRecordGetQuery _rawRecordGetQuery;
    private readonly IResourceImportService _importService;
    private readonly IResourceCandidateBatchAnalysisService _batchAnalysisService;

    public AdminResourceImportController(
        IAdminResourceImportRunListQuery runListQuery,
        IAdminResourceImportRunGetQuery runGetQuery,
        IAdminResourceRawRecordListQuery rawRecordListQuery,
        IAdminResourceRawRecordGetQuery rawRecordGetQuery,
        IResourceImportService importService,
        IResourceCandidateBatchAnalysisService batchAnalysisService)
    {
        _runListQuery = runListQuery;
        _runGetQuery = runGetQuery;
        _rawRecordListQuery = rawRecordListQuery;
        _rawRecordGetQuery = rawRecordGetQuery;
        _importService = importService;
        _batchAnalysisService = batchAnalysisService;
    }

    // GET api/admin/resource-import-runs?page=1&pageSize=20&sourceId=...&status=Completed
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? sourceId = null,
        [FromQuery] string? status = null, CancellationToken ct = default)
    {
        var result = await _runListQuery.HandleAsync(new ListAdminResourceImportRunsQuery(page, pageSize, sourceId, status), ct);
        return Ok(result);
    }

    // GET api/admin/resource-import-runs/{runId}
    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> Get(Guid runId, CancellationToken ct)
    {
        var result = await _runGetQuery.HandleAsync(new GetAdminResourceImportRunQuery(runId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/resource-import-runs  multipart/form-data: sourceId, importMode, file, notes?
    [HttpPost]
    [RequestSizeLimit(LinguaCoach.Infrastructure.ResourceImport.ResourceImportService.MaxFileSizeBytes)]
    public async Task<IActionResult> Import(
        [FromForm] Guid sourceId, [FromForm] string importMode, IFormFile file,
        [FromForm] string? notes, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        if (!Enum.TryParse<ResourceImportMode>(importMode, ignoreCase: true, out var mode))
            return BadRequest(new { error = $"Unsupported import mode '{importMode}'. Use Csv, Json, or Jsonl." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.ImportAsync(new ResourceImportRequest(
                sourceId, stream, file.FileName, mode, GetCurrentUserId(), notes), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/resource-import-runs/{runId}/raw-records?page=1&pageSize=50&extractionStatus=Rejected
    [HttpGet("{runId:guid}/raw-records")]
    public async Task<IActionResult> ListRawRecords(
        Guid runId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? extractionStatus = null, CancellationToken ct = default)
    {
        var result = await _rawRecordListQuery.HandleAsync(
            new ListAdminResourceRawRecordsQuery(runId, page, pageSize, extractionStatus), ct);
        return Ok(result);
    }

    // GET api/admin/resource-import-runs/raw-records/{rawRecordId}
    [HttpGet("raw-records/{rawRecordId:guid}")]
    public async Task<IActionResult> GetRawRecord(Guid rawRecordId, CancellationToken ct)
    {
        var result = await _rawRecordGetQuery.HandleAsync(new GetAdminResourceRawRecordQuery(rawRecordId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/resource-import-runs/{runId}/candidates/analyze
    // Phase E2 — bounded-batch AI analysis + re-validation of all not-yet-analyzed candidates for
    // this run. Capped at IResourceCandidateBatchAnalysisService's MaxCandidatesPerBatch (50) per
    // call — this is deliberately synchronous/batched, not a background job (see that service's
    // doc comment for why). Re-run the same call to sweep the next batch if the cap was reached.
    [HttpPost("{runId:guid}/candidates/analyze")]
    public async Task<IActionResult> AnalyzePendingCandidates(Guid runId, CancellationToken ct)
    {
        var result = await _batchAnalysisService.AnalyzePendingForRunAsync(runId, ct);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

/// <summary>
/// Phase E1 — read-only visibility into staged candidates, plus a limited AdminNotes edit.
/// No approve/reject/publish action exists here by design (Phase E4 scope).
/// </summary>
[ApiController]
[Route("api/admin/resource-candidates")]
[Authorize(Roles = "Admin")]
public sealed class AdminResourceCandidateController : ControllerBase
{
    private readonly IAdminResourceCandidateListQuery _listQuery;
    private readonly IAdminResourceCandidateGetQuery _getQuery;
    private readonly IAdminResourceCandidateNotesHandler _notesHandler;
    private readonly IResourceCandidateAnalysisService _analysisService;
    private readonly IResourceCandidateValidationService _validationService;
    private readonly IResourceCandidatePreviewService _previewService;

    public AdminResourceCandidateController(
        IAdminResourceCandidateListQuery listQuery,
        IAdminResourceCandidateGetQuery getQuery,
        IAdminResourceCandidateNotesHandler notesHandler,
        IResourceCandidateAnalysisService analysisService,
        IResourceCandidateValidationService validationService,
        IResourceCandidatePreviewService previewService)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _notesHandler = notesHandler;
        _analysisService = analysisService;
        _validationService = validationService;
        _previewService = previewService;
    }

    // GET api/admin/resource-candidates?page=1&pageSize=20&sourceId=&importRunId=&candidateType=&
    //   validationStatus=&reviewStatus=&languageCode=&cefrLevel=&search=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? sourceId = null,
        [FromQuery] Guid? importRunId = null, [FromQuery] string? candidateType = null,
        [FromQuery] string? validationStatus = null, [FromQuery] string? reviewStatus = null,
        [FromQuery] string? languageCode = null, [FromQuery] string? cefrLevel = null,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(new ListAdminResourceCandidatesQuery(
            page, pageSize, sourceId, importRunId, candidateType, validationStatus, reviewStatus,
            languageCode, cefrLevel, search), ct);
        return Ok(result);
    }

    // GET api/admin/resource-candidates/{candidateId}
    [HttpGet("{candidateId:guid}")]
    public async Task<IActionResult> Get(Guid candidateId, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetAdminResourceCandidateQuery(candidateId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // PUT api/admin/resource-candidates/{candidateId}/notes  { adminNotes? }
    [HttpPut("{candidateId:guid}/notes")]
    public async Task<IActionResult> SetNotes(Guid candidateId, [FromBody] SetCandidateNotesRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _notesHandler.HandleAsync(
                new SetResourceCandidateAdminNotesCommand(candidateId, request.AdminNotes), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/resource-candidates/{candidateId}/analyze
    // Phase E2 — AI analysis (advisory) followed immediately by a full deterministic
    // re-validation, so the returned candidate's ValidationStatus always reflects the AI's
    // latest suggestion. Idempotent/update-safe — re-running overwrites the prior analysis,
    // never duplicates anything. Never publishes, never deletes a candidate.
    [HttpPost("{candidateId:guid}/analyze")]
    public async Task<IActionResult> Analyze(Guid candidateId, CancellationToken ct)
    {
        var analysisResult = await _analysisService.AnalyzeAsync(candidateId, ct);
        if (!analysisResult.Success && string.Equals(analysisResult.ErrorMessage, "Candidate not found.", StringComparison.Ordinal))
            return NotFound(new { error = analysisResult.ErrorMessage });

        var validationResult = await _validationService.ValidateAsync(candidateId, ct);
        var dto = await _getQuery.HandleAsync(new GetAdminResourceCandidateQuery(candidateId), ct);

        return Ok(new
        {
            candidate = dto,
            analysis = new { analysisResult.Success, analysisResult.ErrorMessage, analysisResult.ProviderName, analysisResult.ModelName },
            validation = validationResult
        });
    }

    // POST api/admin/resource-candidates/{candidateId}/validate
    // Phase E2 — re-runs deterministic rule validation only (no AI call). Useful to re-check a
    // candidate after something external changed, e.g. its source's import approval was revoked.
    [HttpPost("{candidateId:guid}/validate")]
    public async Task<IActionResult> Validate(Guid candidateId, CancellationToken ct)
    {
        try
        {
            var result = await _validationService.ValidateAsync(candidateId, ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // GET api/admin/resource-candidates/{candidateId}/preview
    // Phase E3 — read-only rendered preview: a "what the student would see" projection per
    // candidate type plus an admin-only provenance/validation/AI-analysis summary. Never mutates
    // the candidate and never writes to any published Cefr* bank table. No publish/approve
    // action exists here (Phase E4).
    [HttpGet("{candidateId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid candidateId, CancellationToken ct)
    {
        var result = await _previewService.GetPreviewAsync(candidateId, ct);
        return result is null
            ? NotFound(new { error = $"Resource candidate '{candidateId}' was not found." })
            : Ok(result);
    }

    public sealed record SetCandidateNotesRequest(string? AdminNotes);
}
