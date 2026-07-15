using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Mandatory Import Execution Plan addendum, Parts 5+6+9 — the only path by which a package
// moves from AwaitingMappingApproval to Queued. Approval is always an explicit administrator
// action; there is no pre-checked/implicit approval anywhere in this service. ──

internal sealed class ImportExecutionPlanApprovalService : IImportExecutionPlanApprovalService
{
    private readonly LinguaCoachDbContext _db;

    public ImportExecutionPlanApprovalService(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<ImportExecutionPlanDto> ApproveAsync(ApproveImportExecutionPlanCommand command, CancellationToken ct = default)
    {
        var (package, plan) = await LoadAsync(command.ImportPackageId, command.PlanId, ct);

        var blockingReasons = ValidatePlanQuality(plan);
        if (blockingReasons.Count > 0)
            throw new ImportExecutionPlanNotApprovableException(blockingReasons);

        plan.Approve(command.ApprovedByUserId, DateTimeOffset.UtcNow, command.ApprovedCostCeiling);
        package.ApproveProfile(plan.Id);

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

    private static ImportExecutionPlanDto ToDto(ImportPackage package, ImportProfile plan)
    {
        var estimate = string.IsNullOrEmpty(plan.PlanEstimateJson)
            ? null
            : JsonSerializer.Deserialize<ImportExecutionPlanEstimate>(plan.PlanEstimateJson);

        return new ImportExecutionPlanDto(
            plan.Id, package.Id, plan.Version, plan.Status, package.ProcessingMode, package.ProcessingModeReason,
            estimate!, plan.ApprovedCostCeiling, plan.CreatedAtUtc, plan.ApprovedAtUtc, plan.ApprovedByUserId,
            plan.RejectedAtUtc, plan.RejectionReason, plan.PauseReason, plan.ChangeReason);
    }
}
