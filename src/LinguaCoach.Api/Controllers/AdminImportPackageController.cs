using System.Security.Claims;
using LinguaCoach.Application.ResourceImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase 4.2 (2026-07-15 mandatory Import Execution Plan gate for every import) — the single
/// canonical Import entry point. Every submission — pasted text, one file, several files, or a
/// ZIP archive — becomes an <c>ImportPackage</c> here and must go through plan generation and
/// explicit admin approval before any candidate is created, any AI call runs, or anything
/// publishes. No endpoint anywhere in this codebase may create a Resource Candidate, run AI
/// column-mapping/enrichment, or trigger full-package processing directly — that only happens
/// once a plan reaches Approved (see <c>ImportPackageProcessingService</c>, the background job).
/// </summary>
[ApiController]
[Route("api/admin/import-packages")]
[Authorize(Roles = "Admin")]
public sealed class AdminImportPackageController : ControllerBase
{
    private readonly IImportPackageUploadService _uploadService;
    private readonly IImportUploadSessionService _uploadSessionService;
    private readonly IImportPackageSubmissionService _submissionService;
    private readonly IImportExecutionPlanGenerationService _planGenerationService;
    private readonly IImportExecutionPlanApprovalService _planApprovalService;
    private readonly IImportPlanDraftService _planDraftService;
    private readonly IImportPlanPreviewService _planPreviewService;
    private readonly IImportCostCeilingAmendmentService _costCeilingAmendmentService;
    private readonly IImportSttOperationSummaryQuery _sttOperationSummaryQuery;
    private readonly IImportAiEnrichmentOperationSummaryQuery _aiOperationSummaryQuery;

    public AdminImportPackageController(
        IImportPackageUploadService uploadService,
        IImportUploadSessionService uploadSessionService,
        IImportPackageSubmissionService submissionService,
        IImportExecutionPlanGenerationService planGenerationService,
        IImportExecutionPlanApprovalService planApprovalService,
        IImportPlanDraftService planDraftService,
        IImportPlanPreviewService planPreviewService,
        IImportCostCeilingAmendmentService costCeilingAmendmentService,
        IImportSttOperationSummaryQuery sttOperationSummaryQuery,
        IImportAiEnrichmentOperationSummaryQuery aiOperationSummaryQuery)
    {
        _uploadService = uploadService;
        _uploadSessionService = uploadSessionService;
        _submissionService = submissionService;
        _planGenerationService = planGenerationService;
        _planApprovalService = planApprovalService;
        _planDraftService = planDraftService;
        _planPreviewService = planPreviewService;
        _costCeilingAmendmentService = costCeilingAmendmentService;
        _sttOperationSummaryQuery = sttOperationSummaryQuery;
        _aiOperationSummaryQuery = aiOperationSummaryQuery;
    }

    // POST api/admin/import-packages/submit  multipart/form-data:
    //   cefrResourceSourceId, pastedText?, files[]?, notes?
    // Phase 4.2 — the canonical entry point for pasted content and/or loose (non-ZIP) files. A
    // ZIP archive still uses upload-request/confirm-upload below (presigned direct-to-storage
    // upload, unchanged). Creates the ImportPackage + its ImportAsset(s) + an accepted manifest
    // synchronously; never creates a candidate and never calls AI — that only happens after the
    // plan this package still needs is generated (POST .../plan below) and approved.
    [HttpPost("submit")]
    [RequestSizeLimit(600_000_000)]
    public async Task<IActionResult> Submit(
        [FromForm] Guid cefrResourceSourceId, [FromForm] string? pastedText,
        List<IFormFile>? files, [FromForm] string? notes, CancellationToken ct)
    {
        var fileInputs = new List<ImportPackageSubmissionFile>();
        try
        {
            foreach (var file in files ?? new List<IFormFile>())
            {
                if (file.Length == 0) continue;
                fileInputs.Add(new ImportPackageSubmissionFile(file.FileName, file.OpenReadStream(), file.Length));
            }

            var result = await _submissionService.SubmitAsync(new SubmitImportPackageCommand(
                cefrResourceSourceId, pastedText, fileInputs, notes, CurrentUserId()), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        finally
        {
            foreach (var input in fileInputs) await input.Content.DisposeAsync();
        }
    }

    // POST api/admin/import-packages/upload-request
    [HttpPost("upload-request")]
    public async Task<IActionResult> RequestUpload([FromBody] RequestImportPackageUploadBody body, CancellationToken ct)
    {
        try
        {
            var result = await _uploadService.RequestUploadAsync(new RequestImportPackageUploadCommand(
                body.CefrResourceSourceId, body.OriginalFileName, body.DeclaredSizeBytes, CurrentUserId(), body.Notes), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/import-packages/{packageId}/confirm-upload
    [HttpPost("{packageId:guid}/confirm-upload")]
    public async Task<IActionResult> ConfirmUpload(Guid packageId, CancellationToken ct)
    {
        try
        {
            var result = await _uploadService.ConfirmUploadAsync(new ConfirmImportPackageUploadCommand(packageId), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Phase 4.7 (2026-07-17 reliable large uploads) — resumable, chunked-upload sessions.
    // This is what the Import UI now calls for every ZIP archive, regardless of storage backend.
    // See ImportUploadSessionContracts.cs for the full design rationale. ──

    // POST api/admin/import-packages/upload-sessions
    [HttpPost("upload-sessions")]
    public async Task<IActionResult> CreateUploadSession([FromBody] CreateUploadSessionBody body, CancellationToken ct)
    {
        try
        {
            var result = await _uploadSessionService.CreateAsync(new CreateImportUploadSessionCommand(
                body.CefrResourceSourceId, body.OriginalFileName, body.DeclaredTotalSizeBytes,
                CurrentUserId(), body.DeclaredChecksumSha256, body.Notes), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/import-packages/upload-sessions/{sessionId}/parts/{partNumber}?declaredSizeBytes=...&checksumSha256=...
    // Raw request body = the chunk's bytes. Re-uploading the same partNumber replaces it — this is
    // the "retry a failed part" path, no separate endpoint needed.
    [HttpPut("upload-sessions/{sessionId:guid}/parts/{partNumber:int}")]
    [RequestSizeLimit(64_000_000)]
    public async Task<IActionResult> UploadSessionPart(
        Guid sessionId, int partNumber, [FromQuery] long declaredSizeBytes, [FromQuery] string? checksumSha256, CancellationToken ct)
    {
        try
        {
            var result = await _uploadSessionService.UploadPartAsync(new UploadImportSessionPartCommand(
                sessionId, partNumber, Request.Body, declaredSizeBytes, CurrentUserId(), checksumSha256), ct);
            return Ok(result);
        }
        catch (ImportUploadSessionForbiddenException)
        {
            return Forbid();
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/import-packages/upload-sessions/{sessionId} — current status + which parts
    // have already been received, so a refreshed page can resume without re-uploading them.
    [HttpGet("upload-sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetUploadSessionStatus(Guid sessionId, CancellationToken ct)
    {
        try
        {
            var result = await _uploadSessionService.GetStatusAsync(sessionId, CurrentUserId(), ct);
            return Ok(result);
        }
        catch (ImportUploadSessionForbiddenException)
        {
            return Forbid();
        }
        catch (ResourceImportValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/import-packages/upload-sessions/{sessionId}/complete — idempotent; assembles
    // parts, verifies size/checksum, creates the ImportPackage, and runs inspection. Calling this
    // again after success returns the same package summary without reprocessing.
    [HttpPost("upload-sessions/{sessionId:guid}/complete")]
    public async Task<IActionResult> CompleteUploadSession(Guid sessionId, CancellationToken ct)
    {
        try
        {
            var result = await _uploadSessionService.CompleteAsync(
                new CompleteImportUploadSessionCommand(sessionId, CurrentUserId()), ct);
            return Ok(result);
        }
        catch (ImportUploadSessionForbiddenException)
        {
            return Forbid();
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/import-packages/upload-sessions/{sessionId}/abort
    [HttpPost("upload-sessions/{sessionId:guid}/abort")]
    public async Task<IActionResult> AbortUploadSession(Guid sessionId, CancellationToken ct)
    {
        try
        {
            await _uploadSessionService.AbortAsync(new AbortImportUploadSessionCommand(sessionId, CurrentUserId()), ct);
            return Ok(new { status = "Aborted" });
        }
        catch (ImportUploadSessionForbiddenException)
        {
            return Forbid();
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/import-packages/{packageId}/manifest
    [HttpGet("{packageId:guid}/manifest")]
    public async Task<IActionResult> GetManifest(Guid packageId, CancellationToken ct)
    {
        var result = await _uploadService.GetManifestSummaryAsync(packageId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/import-packages/{packageId}/plan — generates/regenerates the Import
    // Execution Plan. Automatic (deterministic clustering + bounded AI review + cost/time
    // estimate) — no manual sample selection input.
    [HttpPost("{packageId:guid}/plan")]
    public async Task<IActionResult> GeneratePlan(Guid packageId, [FromBody] GeneratePlanBody? body, CancellationToken ct)
    {
        try
        {
            var result = await _planGenerationService.GenerateAsync(
                new GenerateImportExecutionPlanCommand(packageId, body?.ChangeReason), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/import-packages/{packageId}/plan — the current (latest-version) plan.
    [HttpGet("{packageId:guid}/plan")]
    public async Task<IActionResult> GetPlan(Guid packageId, CancellationToken ct)
    {
        var result = await _planApprovalService.GetCurrentPlanAsync(packageId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // PUT api/admin/import-packages/{packageId}/plan/{planId} — Phase 4.4 (Workstream A) — saves
    // an edited draft's group instructions (include/exclude, resource type, field mappings),
    // re-validates, and recalculates the estimate. Only a Draft/AwaitingApproval plan is editable.
    [HttpPut("{packageId:guid}/plan/{planId:guid}")]
    public async Task<IActionResult> UpdatePlanDraft(
        Guid packageId, Guid planId, [FromBody] UpdatePlanDraftBody body, CancellationToken ct)
    {
        try
        {
            var result = await _planDraftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
                packageId, planId, body.ExpectedConcurrencyStamp, body.GroupInstructions), ct);
            return Ok(result);
        }
        catch (ImportPlanConcurrencyConflictException ex)
        {
            return Conflict(new { error = ex.Message, currentConcurrencyStamp = ex.CurrentConcurrencyStamp });
        }
        catch (ImportPlanValidationFailedException ex)
        {
            return BadRequest(new { error = ex.Message, errors = ex.Errors });
        }
        catch (ImportPricingUnavailableException ex)
        {
            return BadRequest(new { error = ex.Message, providerName = ex.ProviderName, modelName = ex.ModelName, operation = ex.Operation });
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/import-packages/{packageId}/plan/{planId}/revise — Phase 4.4 (Workstream A4)
    // — creates a new Draft revision copying an existing (typically Approved) plan's instructions,
    // for the admin to edit and re-approve, without mutating the original approved row. Only
    // allowed before the package has started executing its currently-approved plan.
    [HttpPost("{packageId:guid}/plan/{planId:guid}/revise")]
    public async Task<IActionResult> RevisePlan(Guid packageId, Guid planId, [FromBody] RevisePlanBody body, CancellationToken ct)
    {
        try
        {
            var result = await _planDraftService.ReviseAsync(
                new ReviseApprovedImportPlanCommand(packageId, planId, body.ChangeReason), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/import-packages/{packageId}/plan/preview — Phase 4.4 (Workstream A7) —
    // bounded sample preview of the (unsaved) draft's mapping. Creates no candidates, calls no
    // AI/STT provider — see IImportPlanPreviewService's doc comment.
    [HttpPost("{packageId:guid}/plan/preview")]
    public async Task<IActionResult> PreviewPlanDraft(Guid packageId, [FromBody] PreviewPlanDraftBody body, CancellationToken ct)
    {
        try
        {
            var result = await _planPreviewService.PreviewAsync(new PreviewImportPlanDraftCommand(
                packageId, body.GroupInstructions, body.MaxSampleRowsPerGroup ?? 5), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/import-packages/{packageId}/plan/{planId}/approve — the one and only
    // "Approve and Start Processing" action. Never implicit, never pre-checked.
    [HttpPost("{packageId:guid}/plan/{planId:guid}/approve")]
    public async Task<IActionResult> ApprovePlan(Guid packageId, Guid planId, [FromBody] ApprovePlanBody body, CancellationToken ct)
    {
        try
        {
            var result = await _planApprovalService.ApproveAsync(
                new ApproveImportExecutionPlanCommand(
                    packageId, planId, CurrentUserId(), body.ApprovedCostCeiling, body.ExpectedConcurrencyStamp), ct);
            return Ok(result);
        }
        catch (ImportPlanConcurrencyConflictException ex)
        {
            return Conflict(new { error = ex.Message, currentConcurrencyStamp = ex.CurrentConcurrencyStamp });
        }
        catch (ImportPricingUnavailableException ex)
        {
            return BadRequest(new { error = ex.Message, providerName = ex.ProviderName, modelName = ex.ModelName, operation = ex.Operation });
        }
        catch (ImportExecutionPlanNotApprovableException ex)
        {
            return BadRequest(new { error = ex.Message, blockingReasons = ex.BlockingReasons });
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/import-packages/{packageId}/plan/{planId}/reject
    [HttpPost("{packageId:guid}/plan/{planId:guid}/reject")]
    public async Task<IActionResult> RejectPlan(Guid packageId, Guid planId, [FromBody] RejectPlanBody body, CancellationToken ct)
    {
        try
        {
            var result = await _planApprovalService.RejectAsync(
                new RejectImportExecutionPlanCommand(packageId, planId, CurrentUserId(), body.Reason), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling — Phase 4.4B — the
    // one and only path to raise a paused plan's ceiling and resume it: audited (persists an
    // immutable amendment row), concurrency-checked (409 on a stale ExpectedConcurrencyStamp),
    // and requires the plan to actually be PausedForCostApproval and the new ceiling to exceed
    // the current one. Phase 4.4C removed the prior unaudited approve-revised-ceiling endpoint —
    // see ImportPipelineBoundaryTests for the guard preventing it from returning.
    [HttpPost("{packageId:guid}/plan/{planId:guid}/amend-ceiling")]
    public async Task<IActionResult> AmendCostCeiling(
        Guid packageId, Guid planId, [FromBody] AmendCostCeilingBody body, CancellationToken ct)
    {
        try
        {
            var result = await _costCeilingAmendmentService.AmendAsync(new AmendImportCostCeilingCommand(
                packageId, planId, body.ExpectedConcurrencyStamp, body.NewApprovedCostCeiling, body.Reason, CurrentUserId()), ct);
            return Ok(result);
        }
        catch (ImportPlanConcurrencyConflictException ex)
        {
            return Conflict(new { error = ex.Message, currentConcurrencyStamp = ex.CurrentConcurrencyStamp });
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/import-packages/{packageId}/plan/{planId}/stt-operations — Phase 4.4C —
    // read-only visibility into the durable STT operation ledger for one plan. No provider
    // credentials, no full transcript text; every row is scoped to this exact package + plan.
    [HttpGet("{packageId:guid}/plan/{planId:guid}/stt-operations")]
    public async Task<IActionResult> GetSttOperationSummaries(Guid packageId, Guid planId, CancellationToken ct)
    {
        var result = await _sttOperationSummaryQuery.GetForPlanAsync(packageId, planId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // GET api/admin/import-packages/{packageId}/plan/{planId}/ai-operations — Phase 4.4D —
    // read-only visibility into the durable AI candidate-enrichment operation ledger for one plan.
    // No provider credentials, no raw AI response body; every row is scoped to this exact package
    // + plan.
    [HttpGet("{packageId:guid}/plan/{planId:guid}/ai-operations")]
    public async Task<IActionResult> GetAiOperationSummaries(Guid packageId, Guid planId, CancellationToken ct)
    {
        var result = await _aiOperationSummaryQuery.GetForPlanAsync(packageId, planId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    private Guid? CurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idClaim, out var id) ? id : null;
    }

    public sealed record RequestImportPackageUploadBody(
        Guid CefrResourceSourceId, string OriginalFileName, long DeclaredSizeBytes, string? Notes);

    public sealed record CreateUploadSessionBody(
        Guid CefrResourceSourceId, string OriginalFileName, long DeclaredTotalSizeBytes,
        string? DeclaredChecksumSha256, string? Notes);

    public sealed record GeneratePlanBody(string? ChangeReason);

    public sealed record ApprovePlanBody(decimal ApprovedCostCeiling, Guid ExpectedConcurrencyStamp);

    public sealed record RejectPlanBody(string Reason);

    public sealed record UpdatePlanDraftBody(
        Guid ExpectedConcurrencyStamp, IReadOnlyList<ImportExecutionGroupInstruction> GroupInstructions);

    public sealed record RevisePlanBody(string ChangeReason);

    public sealed record PreviewPlanDraftBody(
        IReadOnlyList<ImportExecutionGroupInstruction> GroupInstructions, int? MaxSampleRowsPerGroup);

    public sealed record AmendCostCeilingBody(
        Guid ExpectedConcurrencyStamp, decimal NewApprovedCostCeiling, string Reason);
}
