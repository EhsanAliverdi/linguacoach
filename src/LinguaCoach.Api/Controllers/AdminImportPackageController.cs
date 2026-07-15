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
    private readonly IImportPackageSubmissionService _submissionService;
    private readonly IImportExecutionPlanGenerationService _planGenerationService;
    private readonly IImportExecutionPlanApprovalService _planApprovalService;
    private readonly IImportPlanDraftService _planDraftService;
    private readonly IImportPlanPreviewService _planPreviewService;

    public AdminImportPackageController(
        IImportPackageUploadService uploadService,
        IImportPackageSubmissionService submissionService,
        IImportExecutionPlanGenerationService planGenerationService,
        IImportExecutionPlanApprovalService planApprovalService,
        IImportPlanDraftService planDraftService,
        IImportPlanPreviewService planPreviewService)
    {
        _uploadService = uploadService;
        _submissionService = submissionService;
        _planGenerationService = planGenerationService;
        _planApprovalService = planApprovalService;
        _planDraftService = planDraftService;
        _planPreviewService = planPreviewService;
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

    // POST api/admin/import-packages/{packageId}/plan/{planId}/approve-revised-ceiling — resumes
    // a plan paused mid-execution because projected cost exceeded the approved ceiling (Part 6).
    [HttpPost("{packageId:guid}/plan/{planId:guid}/approve-revised-ceiling")]
    public async Task<IActionResult> ApproveRevisedCeiling(Guid packageId, Guid planId, [FromBody] ApprovePlanBody body, CancellationToken ct)
    {
        try
        {
            var result = await _planApprovalService.ApproveRevisedCostCeilingAsync(
                new ApproveRevisedCostCeilingCommand(packageId, planId, body.ApprovedCostCeiling), ct);
            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? CurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idClaim, out var id) ? id : null;
    }

    public sealed record RequestImportPackageUploadBody(
        Guid CefrResourceSourceId, string OriginalFileName, long DeclaredSizeBytes, string? Notes);

    public sealed record GeneratePlanBody(string? ChangeReason);

    public sealed record ApprovePlanBody(decimal ApprovedCostCeiling, Guid ExpectedConcurrencyStamp);

    public sealed record RejectPlanBody(string Reason);

    public sealed record UpdatePlanDraftBody(
        Guid ExpectedConcurrencyStamp, IReadOnlyList<ImportExecutionGroupInstruction> GroupInstructions);

    public sealed record RevisePlanBody(string ChangeReason);

    public sealed record PreviewPlanDraftBody(
        IReadOnlyList<ImportExecutionGroupInstruction> GroupInstructions, int? MaxSampleRowsPerGroup);
}
