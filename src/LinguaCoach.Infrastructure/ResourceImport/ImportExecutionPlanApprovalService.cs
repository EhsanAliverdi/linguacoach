using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Mandatory Import Execution Plan addendum, Parts 5+6+9 — the only path by which a package
// moves from AwaitingMappingApproval to Queued. Approval is always an explicit administrator
// action; there is no pre-checked/implicit approval anywhere in this service. Phase 4.4 adds:
// optimistic concurrency (reject a stale ExpectedConcurrencyStamp), fail-closed pricing validation
// (Workstream B7 — a billable plan cannot be approved if required AI pricing doesn't resolve), and
// superseding any other still-Approved plan for the same package when a revision is approved (so
// exactly one plan is ever Approved for a package at a time). ──

internal sealed class ImportExecutionPlanApprovalService : IImportExecutionPlanApprovalService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IAiPricingResolver _pricingResolver;
    private readonly ImportCostEstimationOptions _costOptions;

    public ImportExecutionPlanApprovalService(
        LinguaCoachDbContext db, IAiPricingResolver pricingResolver, IOptions<ImportCostEstimationOptions> costOptions)
    {
        _db = db;
        _pricingResolver = pricingResolver;
        _costOptions = costOptions.Value;
    }

    public async Task<ImportExecutionPlanDto> ApproveAsync(ApproveImportExecutionPlanCommand command, CancellationToken ct = default)
    {
        var (package, plan) = await LoadAsync(command.ImportPackageId, command.PlanId, ct);

        if (plan.ConcurrencyStamp != command.ExpectedConcurrencyStamp)
            throw new ImportPlanConcurrencyConflictException(plan.ConcurrencyStamp);

        var blockingReasons = ValidatePlanQuality(plan);
        if (blockingReasons.Count > 0)
            throw new ImportExecutionPlanNotApprovableException(blockingReasons);

        await ValidatePricingAsync(package, plan, ct);

        plan.Approve(command.ApprovedByUserId, DateTimeOffset.UtcNow, command.ApprovedCostCeiling);
        package.ApproveProfile(plan.Id);

        // Phase 4.4 (Workstream A4) — a revision workflow can leave a prior version still marked
        // Approved (it was approved once, then the admin created+approved a newer revision instead
        // of letting the old one execute) — supersede it now so exactly one plan is ever Approved
        // for this package, matching the invariant plan generation already enforces at generation
        // time for Draft/AwaitingApproval/Approved predecessors.
        var otherApproved = await _db.ImportProfiles
            .Where(p => p.ImportPackageId == package.Id && p.Id != plan.Id && p.Status == ImportProfileStatus.Approved)
            .ToListAsync(ct);
        foreach (var other in otherApproved) other.Supersede();

        await _db.SaveChangesAsync(ct);
        return ToDto(package, plan);
    }

    public async Task<ImportExecutionPlanDto> RejectAsync(RejectImportExecutionPlanCommand command, CancellationToken ct = default)
    {
        var (package, plan) = await LoadAsync(command.ImportPackageId, command.PlanId, ct);

        plan.Reject(command.RejectedByUserId, DateTimeOffset.UtcNow, command.Reason);
        package.MoveToStatus(ImportPackageStatus.Failed);

        await _db.SaveChangesAsync(ct);
        return ToDto(package, plan);
    }

    public async Task<ImportExecutionPlanDto> ApproveRevisedCostCeilingAsync(ApproveRevisedCostCeilingCommand command, CancellationToken ct = default)
    {
        var (package, plan) = await LoadAsync(command.ImportPackageId, command.PlanId, ct);

        plan.ApproveRevisedCeilingAndResume(command.NewApprovedCostCeiling);
        package.MoveToStatus(ImportPackageStatus.Queued);

        await _db.SaveChangesAsync(ct);
        return ToDto(package, plan);
    }

    public async Task<ImportExecutionPlanDto?> GetCurrentPlanAsync(Guid importPackageId, CancellationToken ct = default)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == importPackageId, ct);
        if (package is null) return null;

        var plan = await _db.ImportProfiles
            .Where(p => p.ImportPackageId == importPackageId)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct);
        if (plan is null) return null;

        return ToDto(package, plan);
    }

    private async Task<(ImportPackage Package, ImportProfile Plan)> LoadAsync(Guid packageId, Guid planId, CancellationToken ct)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == packageId, ct)
            ?? throw new ResourceImportValidationException("Import package not found.");
        var plan = await _db.ImportProfiles.FirstOrDefaultAsync(p => p.Id == planId && p.ImportPackageId == packageId, ct)
            ?? throw new ResourceImportValidationException("Import Execution Plan not found.");
        return (package, plan);
    }

    /// <summary>Part 9 — blocks approval outright rather than letting an unactionable plan
    /// through; distinct from the (always-approvable) risk list.</summary>
    private static List<string> ValidatePlanQuality(ImportProfile plan)
    {
        var reasons = new List<string>();

        if (plan.Status != ImportProfileStatus.AwaitingApproval)
            reasons.Add($"Plan is in status '{plan.Status}', not AwaitingApproval.");
        if (plan.EstimatedCandidateCount <= 0)
            reasons.Add("Plan does not estimate any candidates to create.");
        if (string.IsNullOrWhiteSpace(plan.PlanEstimateJson))
            reasons.Add("Plan has no cost/time estimate.");

        return reasons;
    }

    /// <summary>Workstream B7 — "missing pricing must not silently become zero." Checked again
    /// here (on top of whatever ImportPlanEstimateService/ImportExecutionPlanGenerationService
    /// already checked when the estimate was built) because the estimate could have been computed
    /// before a DB pricing override was deactivated, or the plan could be an old revision whose
    /// estimate predates a config change — approval is the last deterministic gate before anything
    /// billable can run.</summary>
    private static readonly string[] StructuredExtensions = { ".csv", ".json", ".jsonl" };

    private async Task ValidatePricingAsync(ImportPackage package, ImportProfile plan, CancellationToken ct)
    {
        if (package.ProcessingMode is null or ImportProcessingMode.Direct) return;
        if (plan.EstimatedCandidateCount <= 0 || string.IsNullOrEmpty(plan.PlanEstimateJson)) return;

        // AI enrichment pricing is only actually needed if the plan will send structured-file
        // candidates through IResourceCandidateBatchAnalysisService (ImportPackageProcessingService
        // only calls that after a successful CSV/JSON/JSONL import — a pure audio/Listening package
        // never reaches it, and must not be blocked over AI pricing it will never spend).
        var estimate = JsonSerializer.Deserialize<ImportExecutionPlanEstimate>(plan.PlanEstimateJson);
        var hasStructuredFiles = estimate?.Volume.FilesByExtension
            .Any(kv => StructuredExtensions.Contains(kv.Key, StringComparer.OrdinalIgnoreCase) && kv.Value > 0) ?? false;
        if (!hasStructuredFiles) return;

        var pricing = await _pricingResolver.ResolveAsync(
            _costOptions.AssumedAiProviderName, _costOptions.AssumedAiModelName, ct);
        if (pricing is null)
            throw new ImportPricingUnavailableException(
                _costOptions.AssumedAiProviderName, _costOptions.AssumedAiModelName, "AI candidate enrichment");
    }

    private static ImportExecutionPlanDto ToDto(ImportPackage package, ImportProfile plan)
    {
        var estimate = string.IsNullOrEmpty(plan.PlanEstimateJson)
            ? null
            : JsonSerializer.Deserialize<ImportExecutionPlanEstimate>(plan.PlanEstimateJson);

        return new ImportExecutionPlanDto(
            plan.Id, package.Id, plan.Version, plan.Status, package.ProcessingMode, package.ProcessingModeReason,
            estimate!, plan.ApprovedCostCeiling, plan.CreatedAtUtc, plan.ApprovedAtUtc, plan.ApprovedByUserId,
            plan.RejectedAtUtc, plan.RejectionReason, plan.PauseReason, plan.ChangeReason,
            plan.ConcurrencyStamp, plan.Status is ImportProfileStatus.Draft or ImportProfileStatus.AwaitingApproval,
            ImportPlanDtoHelpers.DeserializeGroupInstructionsSafe(plan.ProfileJson));
    }
}
