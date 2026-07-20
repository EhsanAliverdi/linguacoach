using System.Security.Claims;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase E1 — resource import runs and raw records. Read-only, and deliberately narrowed further
/// in Phase 4.2: the file-upload Import action, the AI column-mapping "propose" action, and the
/// batch candidate-analysis action all moved behind the mandatory Import Execution Plan gate (see
/// <c>AdminImportPackageController</c>/<c>ImportPackageProcessingService</c>) — every one of them
/// used to be able to create or AI-enrich candidates without any plan/approval, which is exactly
/// the ungated path the Phase 4.1 audit flagged. This controller can no longer create, mutate, or
/// AI-analyze anything.
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

    public AdminResourceImportController(
        IAdminResourceImportRunListQuery runListQuery,
        IAdminResourceImportRunGetQuery runGetQuery,
        IAdminResourceRawRecordListQuery rawRecordListQuery,
        IAdminResourceRawRecordGetQuery rawRecordGetQuery)
    {
        _runListQuery = runListQuery;
        _runGetQuery = runGetQuery;
        _rawRecordListQuery = rawRecordListQuery;
        _rawRecordGetQuery = rawRecordGetQuery;
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
    private readonly IAdminResourceCandidateReviewSummaryQuery _reviewSummaryQuery;
    private readonly IAdminResourceCandidateNotesHandler _notesHandler;
    private readonly IResourceCandidateAnalysisService _analysisService;
    private readonly IResourceCandidateValidationService _validationService;
    private readonly IResourceCandidatePreviewService _previewService;
    private readonly IAdminResourceCandidateApproveHandler _approveHandler;
    private readonly IAdminResourceCandidateRejectHandler _rejectHandler;
    private readonly IAdminResourceCandidateSkipHandler _skipHandler;
    private readonly IAdminResourceCandidateContentUpdateHandler _contentUpdateHandler;
    private readonly IResourceCandidatePublishService _publishService;
    private readonly IResourceCandidateBatchActionService _batchActionService;
    private readonly IResourceCandidateAudioService _audioService;
    private readonly IResourceCandidateOrphanRepairService _orphanRepairService;

    public AdminResourceCandidateController(
        IAdminResourceCandidateListQuery listQuery,
        IAdminResourceCandidateGetQuery getQuery,
        IAdminResourceCandidateReviewSummaryQuery reviewSummaryQuery,
        IAdminResourceCandidateNotesHandler notesHandler,
        IResourceCandidateAnalysisService analysisService,
        IResourceCandidateValidationService validationService,
        IResourceCandidatePreviewService previewService,
        IAdminResourceCandidateApproveHandler approveHandler,
        IAdminResourceCandidateRejectHandler rejectHandler,
        IAdminResourceCandidateSkipHandler skipHandler,
        IAdminResourceCandidateContentUpdateHandler contentUpdateHandler,
        IResourceCandidatePublishService publishService,
        IResourceCandidateBatchActionService batchActionService,
        IResourceCandidateAudioService audioService,
        IResourceCandidateOrphanRepairService orphanRepairService)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _reviewSummaryQuery = reviewSummaryQuery;
        _notesHandler = notesHandler;
        _analysisService = analysisService;
        _validationService = validationService;
        _previewService = previewService;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _skipHandler = skipHandler;
        _contentUpdateHandler = contentUpdateHandler;
        _publishService = publishService;
        _batchActionService = batchActionService;
        _audioService = audioService;
        _orphanRepairService = orphanRepairService;
    }

    // GET api/admin/resource-candidates?page=1&pageSize=20&sourceId=&importRunId=&candidateType=&
    //   validationStatus=&reviewStatus=&languageCode=&cefrLevel=&search=&publishableOnly=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? sourceId = null,
        [FromQuery] Guid? importRunId = null, [FromQuery] string? candidateType = null,
        [FromQuery] string? validationStatus = null, [FromQuery] string? reviewStatus = null,
        [FromQuery] string? languageCode = null, [FromQuery] string? cefrLevel = null,
        [FromQuery] string? search = null, [FromQuery] bool? publishableOnly = null, CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(new ListAdminResourceCandidatesQuery(
            page, pageSize, sourceId, importRunId, candidateType, validationStatus, reviewStatus,
            languageCode, cefrLevel, search, publishableOnly), ct);
        return Ok(result);
    }

    // GET api/admin/resource-candidates/summary?importRunId=&sourceId=
    // Phase K2 — headline review-state counts for the Import Content page (passed / needs review /
    // blocked / published), so "Analysis complete" no longer has to stand in for "ready to publish."
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] Guid? importRunId = null, [FromQuery] Guid? sourceId = null, CancellationToken ct = default)
    {
        var result = await _reviewSummaryQuery.HandleAsync(
            new GetAdminResourceCandidateReviewSummaryQuery(importRunId, sourceId), ct);
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

    // PUT api/admin/resource-candidates/{candidateId}/content
    // Phase 3 (2026-07-15 import candidate review workflow) — edits a staged candidate's content
    // before approval. Re-validates immediately after the edit so the returned DTO's
    // ValidationStatus/CanAttemptPublish reflect the new content, not stale pre-edit gates.
    // Rejected for an already-published candidate — edit through the Resource Bank instead.
    [HttpPut("{candidateId:guid}/content")]
    public async Task<IActionResult> UpdateContent(
        Guid candidateId, [FromBody] UpdateCandidateContentRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _contentUpdateHandler.HandleAsync(new UpdateResourceCandidateContentCommand(
                candidateId, request.CanonicalText, request.NormalizedJson, request.TypedContentJson, request.CefrLevel,
                request.PrimarySkill, request.Subskill, request.DifficultyBand,
                request.ContextTags, request.FocusTags), ct);
            return Ok(result);
        }
        catch (CandidateContentValidationException ex)
        {
            return BadRequest(new { error = ex.Message, fieldErrors = ex.Errors });
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
        catch (CandidateContentValidationException ex)
        {
            return BadRequest(new { error = ex.Message, fieldErrors = ex.Errors });
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

    // POST api/admin/resource-candidates/{candidateId}/skip  { reason? }
    // Phase 3 (2026-07-15 import candidate review workflow) — "I am intentionally ignoring this
    // candidate," distinct from PendingReview (never reviewed). Reason is optional, unlike reject.
    // Blocked outright for an already-published candidate.
    [HttpPost("{candidateId:guid}/skip")]
    public async Task<IActionResult> Skip(Guid candidateId, [FromBody] SkipCandidateRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _skipHandler.HandleAsync(new SkipResourceCandidateCommand(candidateId, request.Reason), ct);
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

    // POST api/admin/resource-candidates/batch/approve  { candidateIds: [...], notes? }
    // Phase K2 — batch admin approval over an explicit set of candidates (the current filtered
    // page's selection). Never publishes anything by itself.
    [HttpPost("batch/approve")]
    public async Task<IActionResult> BatchApprove([FromBody] BatchCandidateIdsRequest request, CancellationToken ct)
    {
        var result = await _batchActionService.ApproveAsync(
            new BatchApproveResourceCandidatesCommand(request.CandidateIds, request.Notes), ct);
        return Ok(result);
    }

    // POST api/admin/resource-candidates/batch/publish  { candidateIds: [...] }
    // Phase K2 — batch publish over an explicit set of already-approved candidates. Already-
    // published candidates in the request are treated as a safe no-op (see
    // BatchResourceCandidateActionResult.AlreadyPublishedCount), never a duplicate bank row.
    [HttpPost("batch/publish")]
    public async Task<IActionResult> BatchPublish([FromBody] BatchCandidateIdsRequest request, CancellationToken ct)
    {
        var result = await _batchActionService.PublishAsync(
            new BatchPublishResourceCandidatesCommand(request.CandidateIds), GetCurrentUserId(), ct);
        return Ok(result);
    }

    // POST api/admin/resource-candidates/repair-orphaned-publish
    // Platform Reliability Sprint 8.1 — one-time repair for candidates whose publish reference was
    // orphaned by the Phase I0 "drop typed bank tables" migration (see
    // IResourceCandidateOrphanRepairService for the full rationale). Idempotent: candidates that no
    // longer match the dead-type signature are simply not returned on a re-run.
    [HttpPost("repair-orphaned-publish")]
    public async Task<IActionResult> RepairOrphanedPublish(CancellationToken ct)
    {
        var result = await _orphanRepairService.RepairOrphanedPublishReferencesAsync(ct);
        return Ok(result);
    }

    // POST api/admin/resource-candidates/batch/reject  { candidateIds: [...], reason }
    // Phase 3 (2026-07-15 import candidate review workflow).
    [HttpPost("batch/reject")]
    public async Task<IActionResult> BatchReject([FromBody] BatchCandidateReasonRequest request, CancellationToken ct)
    {
        var result = await _batchActionService.RejectAsync(
            new BatchRejectResourceCandidatesCommand(request.CandidateIds, request.Reason ?? string.Empty), ct);
        return Ok(result);
    }

    // POST api/admin/resource-candidates/batch/skip  { candidateIds: [...], reason? }
    // Phase 3 (2026-07-15 import candidate review workflow).
    [HttpPost("batch/skip")]
    public async Task<IActionResult> BatchSkip([FromBody] BatchCandidateReasonRequest request, CancellationToken ct)
    {
        var result = await _batchActionService.SkipAsync(
            new BatchSkipResourceCandidatesCommand(request.CandidateIds, request.Reason), ct);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public sealed record SetCandidateNotesRequest(string? AdminNotes);
    public sealed record ApproveCandidateRequest(string? Notes = null);
    public sealed record RejectCandidateRequest(string Reason);
    public sealed record SkipCandidateRequest(string? Reason = null);
    public sealed record UpdateCandidateContentRequest(
        string? CanonicalText = null,
        string? NormalizedJson = null,
        string? TypedContentJson = null,
        string? CefrLevel = null,
        string? PrimarySkill = null,
        string? Subskill = null,
        int? DifficultyBand = null,
        IReadOnlyList<string>? ContextTags = null,
        IReadOnlyList<string>? FocusTags = null
    );
    public sealed record BatchCandidateIdsRequest(IReadOnlyList<Guid> CandidateIds, string? Notes = null);
    public sealed record BatchCandidateReasonRequest(IReadOnlyList<Guid> CandidateIds, string? Reason = null);
}
