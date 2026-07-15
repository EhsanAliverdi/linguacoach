using System.Security.Claims;
using LinguaCoach.Application.ResourceImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Mandatory Import Execution Plan addendum (2026-07-15) — the large-scale package lifecycle:
/// request a signed upload URL, confirm the upload (triggers manifest inspection), generate the
/// automatic Import Execution Plan, and approve/reject/resume it. No endpoint here can trigger
/// full-package processing directly — that only happens once a plan reaches Approved (Part 8's
/// background job, wired separately).
/// </summary>
[ApiController]
[Route("api/admin/import-packages")]
[Authorize(Roles = "Admin")]
public sealed class AdminImportPackageController : ControllerBase
{
    private readonly IImportPackageUploadService _uploadService;
    private readonly IImportExecutionPlanGenerationService _planGenerationService;
    private readonly IImportExecutionPlanApprovalService _planApprovalService;

    public AdminImportPackageController(
        IImportPackageUploadService uploadService,
        IImportExecutionPlanGenerationService planGenerationService,
        IImportExecutionPlanApprovalService planApprovalService)
    {
        _uploadService = uploadService;
        _planGenerationService = planGenerationService;
        _planApprovalService = planApprovalService;
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

    // POST api/admin/import-packages/{packageId}/plan/{planId}/approve — the one and only
    // "Approve and Start Processing" action. Never implicit, never pre-checked.
    [HttpPost("{packageId:guid}/plan/{planId:guid}/approve")]
    public async Task<IActionResult> ApprovePlan(Guid packageId, Guid planId, [FromBody] ApprovePlanBody body, CancellationToken ct)
    {
        try
        {
            var result = await _planApprovalService.ApproveAsync(
                new ApproveImportExecutionPlanCommand(packageId, planId, CurrentUserId(), body.ApprovedCostCeiling), ct);
            return Ok(result);
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

    public sealed record ApprovePlanBody(decimal ApprovedCostCeiling);

    public sealed record RejectPlanBody(string Reason);
}
