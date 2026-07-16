using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4.4 (Workstream A) — admin editing of a not-yet-approved plan. Every edit re-validates
// through the exact same ImportPlanInstructionValidator execution uses (Phase 4.3's
// ApprovedImportProfileResolver) and recalculates the estimate through IImportPlanEstimateService,
// so a saved draft can never diverge from what execution would actually do, and its displayed
// estimate can never go stale relative to the mapping it describes. ──

internal sealed class ImportPlanDraftService : IImportPlanDraftService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IImportPlanEstimateService _estimateService;

    public ImportPlanDraftService(LinguaCoachDbContext db, IImportPlanEstimateService estimateService)
    {
        _db = db;
        _estimateService = estimateService;
    }

    public async Task<ImportExecutionPlanDto> UpdateDraftAsync(UpdateImportPlanDraftCommand command, CancellationToken ct = default)
    {
        var (package, plan) = await LoadEditableAsync(command.ImportPackageId, command.PlanId, command.ExpectedConcurrencyStamp, ct);

        var manifest = TryDeserializeManifest(package.ManifestJson);
        var errors = ImportPlanInstructionValidator.Validate(command.GroupInstructions, manifest);
        if (errors.Count > 0)
            throw new ImportPlanValidationFailedException(errors);

        var estimate = await _estimateService.RecalculateAsync(package, command.GroupInstructions, ct);

        plan.ReviseDraft(
            JsonSerializer.Serialize(command.GroupInstructions),
            estimate.Volume.ExpectedCandidateCount,
            estimate.Cost.ExpectedCost, estimate.Cost.MinCost, estimate.Cost.MaxCost,
            JsonSerializer.Serialize(estimate));

        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(package, plan, estimate, ct);
    }

    public async Task<ImportExecutionPlanDto> ReviseAsync(ReviseApprovedImportPlanCommand command, CancellationToken ct = default)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == command.ImportPackageId, ct)
            ?? throw new ResourceImportValidationException("Import package not found.");
        var sourcePlan = await _db.ImportProfiles.FirstOrDefaultAsync(
            p => p.Id == command.SourcePlanId && p.ImportPackageId == command.ImportPackageId, ct)
            ?? throw new ResourceImportValidationException("Import Execution Plan not found.");

        // Workstream A4 — revision is a deliberate, explicit workflow, not an automatic reset.
        // Only allowed before the package has actually started executing the currently-approved
        // plan: once extraction/mapping/candidate-creation has begun (or finished), replacing the
        // approved plan out from under it would be exactly the destructive mid-execution reset the
        // phase brief prohibits inventing.
        if (package.Status is not (ImportPackageStatus.AwaitingMappingApproval or ImportPackageStatus.Queued))
            throw new ResourceImportValidationException(
                $"Cannot create a new plan revision — package processing is already in status " +
                $"'{package.Status}'. A revision is only allowed before execution has started.");

        var priorPlans = await _db.ImportProfiles
            .Where(p => p.ImportPackageId == package.Id)
            .OrderByDescending(p => p.Version)
            .ToListAsync(ct);
        var nextVersion = priorPlans.Count == 0 ? 1 : priorPlans.Max(p => p.Version) + 1;

        var revision = new ImportProfile(
            package.Id, nextVersion, sourcePlan.ProfileJson, sourcePlan.SampleAssetIds,
            sourcePlan.EstimatedCandidateCount, DateTimeOffset.UtcNow,
            sourcePlan.AiProviderName, sourcePlan.AiModelName,
            sourcePlan.EstimatedCostExpected, sourcePlan.EstimatedCostMin, sourcePlan.EstimatedCostMax,
            sourcePlan.Currency, sourcePlan.PlanEstimateJson, sourcePlan.PricingSnapshotJson,
            changeReason: command.ChangeReason);
        // Immediately AwaitingApproval, matching plan generation's own lifecycle (Draft only ever
        // exists transiently between construction and SubmitForApproval — there is no separate
        // "propose" step). ImportProfile.ReviseDraft (used by UpdateDraftAsync) accepts edits in
        // either Draft or AwaitingApproval, so this does not block the admin from still editing
        // this revision before approving it.
        revision.SubmitForApproval();

        _db.ImportProfiles.Add(revision);
        await _db.SaveChangesAsync(ct);

        var estimate = string.IsNullOrEmpty(revision.PlanEstimateJson)
            ? null
            : JsonSerializer.Deserialize<ImportExecutionPlanEstimate>(revision.PlanEstimateJson);
        return await ToDtoAsync(package, revision, estimate, ct);
    }

    private async Task<(ImportPackage Package, ImportProfile Plan)> LoadEditableAsync(
        Guid packageId, Guid planId, Guid expectedConcurrencyStamp, CancellationToken ct)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == packageId, ct)
            ?? throw new ResourceImportValidationException("Import package not found.");
        var plan = await _db.ImportProfiles.FirstOrDefaultAsync(p => p.Id == planId && p.ImportPackageId == packageId, ct)
            ?? throw new ResourceImportValidationException("Import Execution Plan not found.");

        if (plan.ConcurrencyStamp != expectedConcurrencyStamp)
            throw new ImportPlanConcurrencyConflictException(plan.ConcurrencyStamp);

        if (plan.Status is not (ImportProfileStatus.Draft or ImportProfileStatus.AwaitingApproval))
            throw new ResourceImportValidationException(
                $"Plan is in status '{plan.Status}' — only a Draft or AwaitingApproval plan can be edited.");

        return (package, plan);
    }

    private static ImportPackageManifest? TryDeserializeManifest(string? manifestJson)
    {
        if (string.IsNullOrEmpty(manifestJson)) return null;
        try { return JsonSerializer.Deserialize<ImportPackageManifest>(manifestJson); }
        catch (JsonException) { return null; }
    }

    private async Task<ImportExecutionPlanDto> ToDtoAsync(
        ImportPackage package, ImportProfile plan, ImportExecutionPlanEstimate? estimate, CancellationToken ct)
    {
        var amendments = await ImportPlanDtoHelpers.LoadCeilingAmendmentsAsync(_db, plan.Id, ct);
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
