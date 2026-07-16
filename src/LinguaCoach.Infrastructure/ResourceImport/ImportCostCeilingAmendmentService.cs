using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4.4B — the audited, concurrency-checked replacement for the pre-4.4B
// ApproveRevisedCostCeilingAsync path. "Cost ceiling may only be increased through an explicit
// audited admin action" — every successful call here both raises the ceiling AND persists an
// immutable ImportCostCeilingAmendment row in the same SaveChangesAsync, so the two can never
// drift apart (no amendment without a ceiling change, no ceiling change without an amendment). ──

internal sealed class ImportCostCeilingAmendmentService : IImportCostCeilingAmendmentService
{
    private readonly LinguaCoachDbContext _db;

    public ImportCostCeilingAmendmentService(LinguaCoachDbContext db) => _db = db;

    public async Task<ImportExecutionPlanDto> AmendAsync(AmendImportCostCeilingCommand command, CancellationToken ct = default)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == command.ImportPackageId, ct)
            ?? throw new ResourceImportValidationException("Import package not found.");
        var plan = await _db.ImportProfiles.FirstOrDefaultAsync(p => p.Id == command.PlanId && p.ImportPackageId == command.ImportPackageId, ct)
            ?? throw new ResourceImportValidationException("Import Execution Plan not found.");

        // Concurrency is checked before any other validation — a stale request must always surface
        // as a conflict, not be silently rejected as "not paused" against data the caller never saw.
        if (plan.ConcurrencyStamp != command.ExpectedConcurrencyStamp)
            throw new ImportPlanConcurrencyConflictException(plan.ConcurrencyStamp);

        if (plan.Status != ImportProfileStatus.PausedForCostApproval)
            throw new ResourceImportValidationException(
                $"Cannot amend the cost ceiling — this plan is in status '{plan.Status}', not paused for cost approval.");

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ResourceImportValidationException("A reason is required to amend the approved cost ceiling.");

        var previousCeiling = plan.ApprovedCostCeiling ?? 0m;
        if (command.NewApprovedCostCeiling <= previousCeiling)
            throw new ResourceImportValidationException(
                $"The new ceiling ({command.NewApprovedCostCeiling:N4}) must be greater than the current approved " +
                $"ceiling ({previousCeiling:N4}).");

        var amendment = new ImportCostCeilingAmendment(
            package.Id, plan.Id, previousCeiling, command.NewApprovedCostCeiling, plan.Currency,
            command.Reason.Trim(), command.AdministratorUserId, DateTimeOffset.UtcNow);
        _db.ImportCostCeilingAmendments.Add(amendment);

        // Resume is a direct consequence of a successfully validated, now-persisted amendment —
        // never automatic, and never reachable without first passing every check above.
        plan.ApproveRevisedCeilingAndResume(command.NewApprovedCostCeiling);
        package.MoveToStatus(ImportPackageStatus.Queued);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A second request committed first (its UPDATE changed concurrency_stamp away from the
            // value both requests read) — this request's own UPDATE matched zero rows. Detach the
            // now-conflicted tracked entities and report the row's current stamp for a clean reload.
            _db.ChangeTracker.Clear();
            var currentStamp = await _db.ImportProfiles
                .Where(p => p.Id == plan.Id)
                .Select(p => p.ConcurrencyStamp)
                .FirstAsync(ct);
            throw new ImportPlanConcurrencyConflictException(currentStamp);
        }

        var amendments = await ImportPlanDtoHelpers.LoadCeilingAmendmentsAsync(_db, plan.Id, ct);
        var estimate = string.IsNullOrEmpty(plan.PlanEstimateJson)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ImportExecutionPlanEstimate>(plan.PlanEstimateJson);

        return new ImportExecutionPlanDto(
            plan.Id, package.Id, plan.Version, plan.Status, package.ProcessingMode, package.ProcessingModeReason,
            estimate!, plan.ApprovedCostCeiling, plan.CreatedAtUtc, plan.ApprovedAtUtc, plan.ApprovedByUserId,
            plan.RejectedAtUtc, plan.RejectionReason, plan.PauseReason, plan.ChangeReason,
            plan.ConcurrencyStamp, plan.Status is ImportProfileStatus.Draft or ImportProfileStatus.AwaitingApproval,
            ImportPlanDtoHelpers.DeserializeGroupInstructionsSafe(plan.ProfileJson),
            package.AccruedCost, package.AccruedCostCurrency,
            plan.ApprovedCostCeiling.HasValue ? plan.ApprovedCostCeiling.Value - package.AccruedCost : null,
            amendments);
    }
}
