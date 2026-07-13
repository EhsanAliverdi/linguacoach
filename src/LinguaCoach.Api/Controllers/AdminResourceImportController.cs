using System.Security.Claims;
using System.Text.Json;
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
    private readonly IResourceImportColumnMappingService _columnMappingService;

    public AdminResourceImportController(
        IAdminResourceImportRunListQuery runListQuery,
        IAdminResourceImportRunGetQuery runGetQuery,
        IAdminResourceRawRecordListQuery rawRecordListQuery,
        IAdminResourceRawRecordGetQuery rawRecordGetQuery,
        IResourceImportService importService,
        IResourceCandidateBatchAnalysisService batchAnalysisService,
        IResourceImportColumnMappingService columnMappingService)
    {
        _runListQuery = runListQuery;
        _runGetQuery = runGetQuery;
        _rawRecordListQuery = rawRecordListQuery;
        _rawRecordGetQuery = rawRecordGetQuery;
        _importService = importService;
        _batchAnalysisService = batchAnalysisService;
        _columnMappingService = columnMappingService;
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

    // POST api/admin/resource-import-runs  multipart/form-data: sourceId, importMode, file, notes?,
    // columnRenamesJson? (Phase K1 — an admin-confirmed {"sourceColumn":"recognizedField",...} map)
    [HttpPost]
    [RequestSizeLimit(LinguaCoach.Infrastructure.ResourceImport.ResourceImportService.MaxFileSizeBytes)]
    public async Task<IActionResult> Import(
        [FromForm] Guid sourceId, [FromForm] string importMode, IFormFile file,
        [FromForm] string? notes, [FromForm] string? columnRenamesJson, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        if (!Enum.TryParse<ResourceImportMode>(importMode, ignoreCase: true, out var mode))
            return BadRequest(new { error = $"Unsupported import mode '{importMode}'. Use Csv, Json, or Jsonl." });

        IReadOnlyDictionary<string, string>? columnRenames;
        try
        {
            columnRenames = ParseColumnRenames(columnRenamesJson);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "columnRenamesJson is not valid JSON." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.ImportAsync(new ResourceImportRequest(
                sourceId, stream, file.FileName, mode, GetCurrentUserId(), notes, ColumnRenames: columnRenames), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/resource-import-runs/propose-mapping  multipart/form-data: importMode, file
    // Phase K1 — AI-assisted column-mapping proposal. Parses only the header + a bounded sample of
    // rows, never stages anything, never writes to the database. The admin reviews/confirms the
    // result before it's ever sent back as columnRenamesJson on the real Import call above.
    [HttpPost("propose-mapping")]
    [RequestSizeLimit(LinguaCoach.Infrastructure.ResourceImport.ResourceImportService.MaxFileSizeBytes)]
    public async Task<IActionResult> ProposeMapping(
        [FromForm] string importMode, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        if (!Enum.TryParse<ResourceImportMode>(importMode, ignoreCase: true, out var mode))
            return BadRequest(new { error = $"Unsupported import mode '{importMode}'. Use Csv, Json, or Jsonl." });

        string fileText;
        using (var reader = new StreamReader(file.OpenReadStream()))
            fileText = await reader.ReadToEndAsync(ct);

        (IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> SampleRows) sample;
        try
        {
            sample = _importService.ParseSample(fileText, mode);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return BadRequest(new { error = $"File could not be parsed as {mode}: {ex.Message}" });
        }

        var result = await _columnMappingService.ProposeMappingAsync(
            new ResourceImportColumnMappingRequest(sample.Columns, sample.SampleRows), ct);
        return Ok(result);
    }

    private static IReadOnlyDictionary<string, string>? ParseColumnRenames(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(json);

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
/// Phase E4 adds the approve/reject/publish workflow (see the Approve/Reject/Publish actions
/// below) — publish is the only action here that ever writes to a Cefr* bank table.
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
    private readonly IAdminResourceCandidateApproveHandler _approveHandler;
    private readonly IAdminResourceCandidateRejectHandler _rejectHandler;
    private readonly IResourceCandidatePublishService _publishService;
    private readonly IResourceCandidateAudioService _audioService;

    public AdminResourceCandidateController(
        IAdminResourceCandidateListQuery listQuery,
        IAdminResourceCandidateGetQuery getQuery,
        IAdminResourceCandidateNotesHandler notesHandler,
        IResourceCandidateAnalysisService analysisService,
        IResourceCandidateValidationService validationService,
        IResourceCandidatePreviewService previewService,
        IAdminResourceCandidateApproveHandler approveHandler,
        IAdminResourceCandidateRejectHandler rejectHandler,
        IResourceCandidatePublishService publishService,
        IResourceCandidateAudioService audioService)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _notesHandler = notesHandler;
        _analysisService = analysisService;
        _validationService = validationService;
        _previewService = previewService;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _publishService = publishService;
        _audioService = audioService;
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

    // POST api/admin/resource-candidates/{candidateId}/audio  multipart/form-data: audioFile
    // Phase J5c — uploads the real audio file backing a ListeningPassage candidate. Separate from
    // staging (which only carries title/transcript text) — publish is blocked until this has run.
    [HttpPost("{candidateId:guid}/audio")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB hard ceiling, matches the speaking-audio precedent.
    public async Task<IActionResult> UploadAudio(Guid candidateId, IFormFile audioFile, CancellationToken ct)
    {
        if (audioFile is null || audioFile.Length == 0)
            return BadRequest(new { error = "Audio file is required." });

        var mimeType = audioFile.ContentType?.Split(';')[0].Trim() ?? string.Empty;
        if (!_audioService.IsAllowedMimeType(mimeType))
            return BadRequest(new { error = $"Audio format '{mimeType}' is not supported. Use webm, wav, mp3, mp4, m4a, or ogg." });

        if (audioFile.Length > _audioService.GetMaxAudioBytes())
            return BadRequest(new { error = $"Audio file is too large. Maximum size is {_audioService.GetMaxAudioBytes() / (1024 * 1024)} MB." });

        try
        {
            await using var stream = audioFile.OpenReadStream();
            var result = await _audioService.UploadAsync(candidateId, stream, mimeType, ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/resource-candidates/{candidateId}/audio-url
    // Phase J5c — short-lived signed URL (or, for local storage, the authenticated streaming
    // endpoint below) for a ListeningPassage candidate's uploaded audio. Response: { url, expiresAt }.
    [HttpGet("{candidateId:guid}/audio-url")]
    public async Task<IActionResult> GetAudioUrl(Guid candidateId, CancellationToken ct)
    {
        var result = await _audioService.GetAudioUrlAsync(candidateId, ct);
        return result is null ? NotFound(new { error = "No audio file has been uploaded for this candidate." }) : Ok(result);
    }

    // GET api/admin/resource-candidates/{candidateId}/audio
    // Phase J5c — raw audio stream, used as the local-storage fallback when GetAudioUrl's signed
    // URL isn't a directly-fetchable client URL (see IFileStorageService.GenerateSignedUrlAsync).
    [HttpGet("{candidateId:guid}/audio")]
    public async Task<IActionResult> GetAudio(Guid candidateId, CancellationToken ct)
    {
        var result = await _audioService.GetAudioStreamAsync(candidateId, ct);
        return result is null ? NotFound() : File(result.Bytes, result.ContentType);
    }

    // POST api/admin/resource-candidates/{candidateId}/approve  { notes? }
    // Phase E4 — admin approval step, separate from ValidationStatus (deterministic). Never
    // publishes anything by itself.
    [HttpPost("{candidateId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid candidateId, [FromBody] ApproveCandidateRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _approveHandler.HandleAsync(new ApproveResourceCandidateCommand(candidateId, request.Notes), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/resource-candidates/{candidateId}/reject  { reason }
    // Phase E4 — admin rejection. Reason is required. Blocked outright for an already-published
    // candidate (no unpublish step exists in this phase).
    [HttpPost("{candidateId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid candidateId, [FromBody] RejectCandidateRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _rejectHandler.HandleAsync(new RejectResourceCandidateCommand(candidateId, request.Reason), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/resource-candidates/{candidateId}/publish
    // Phase E4 — publishes an approved, validated candidate into its target Cefr* bank table.
    // Idempotent (repeated calls after a successful publish return the same target reference, no
    // duplicate row). A failed publish returns 200 with Success=false and a list of reasons — this
    // mirrors the shape admins need to see every unmet gate at once, not just the first one; a
    // not-found candidate id is the only case that returns 404.
    [HttpPost("{candidateId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid candidateId, CancellationToken ct)
    {
        try
        {
            var result = await _publishService.PublishAsync(candidateId, GetCurrentUserId(), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/resource-candidates/{candidateId}/approve-and-publish  { notes? }
    // Phase I1 — the single-click unified pipeline action: approves (idempotent — a no-op re-set
    // of ReviewStatus if already Approved) then immediately publishes. PublishAsync already
    // re-validates every other gate live (English-only, source approval/license, deterministic
    // validation), so approval was the only precondition a separate click used to gate — this
    // collapses that into one admin action without skipping any check.
    [HttpPost("{candidateId:guid}/approve-and-publish")]
    public async Task<IActionResult> ApproveAndPublish(Guid candidateId, [FromBody] ApproveCandidateRequest request, CancellationToken ct)
    {
        try
        {
            await _approveHandler.HandleAsync(new ApproveResourceCandidateCommand(candidateId, request.Notes), ct);
            var result = await _publishService.PublishAsync(candidateId, GetCurrentUserId(), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public sealed record SetCandidateNotesRequest(string? AdminNotes);
    public sealed record ApproveCandidateRequest(string? Notes = null);
    public sealed record RejectCandidateRequest(string Reason);
}
