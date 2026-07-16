namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.4B — the audited "cost ceiling may only be increased through an explicit audited
// admin action" workflow. Reuses ImportPlanConcurrencyConflictException for a stale amendment
// (same 409 semantics as draft-update/approve) and ResourceImportValidationException for every
// other rejection (package/plan not found, not paused for cost, ceiling not raised, blank reason) —
// consistent with the rest of this controller's error mapping. ──

public sealed record AmendImportCostCeilingCommand(
    Guid ImportPackageId,
    Guid PlanId,
    Guid ExpectedConcurrencyStamp,
    decimal NewApprovedCostCeiling,
    string Reason,
    Guid? AdministratorUserId);

/// <summary>One persisted, immutable amendment audit row, as returned to the admin UI.</summary>
public sealed record ImportCostCeilingAmendmentDto(
    Guid AmendmentId,
    decimal PreviousCeiling,
    decimal NewCeiling,
    string Currency,
    string Reason,
    Guid? AdministratorUserId,
    DateTimeOffset CreatedAtUtc);

public interface IImportCostCeilingAmendmentService
{
    /// <summary>Validates the package is paused specifically for cost, the new ceiling exceeds the
    /// current one, the reason is non-blank, and the concurrency stamp is current — then, in one
    /// save, persists the audit row, raises the plan's ceiling, and resumes the package. Never a
    /// silent/automatic resume: this is the only path back from PausedForCostApproval.</summary>
    Task<ImportExecutionPlanDto> AmendAsync(AmendImportCostCeilingCommand command, CancellationToken ct = default);
}
