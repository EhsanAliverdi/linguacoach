namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4 (2026-07-15), Part 8 — the only path that may create candidates from a package's
// files. Requires an Approved (or PausedForCostApproval→resumed) plan; enforces the plan's
// approved cost ceiling while it runs, pausing (not silently continuing) before a projected
// overspend. Processes one package at a time, checkpointed per pipeline stage so a retry never
// restarts from the first file. ──

public sealed record ImportPackageProcessingOutcome(
    Guid ImportPackageId,
    bool Completed,
    bool PausedForCostApproval,
    string? PauseOrFailureReason);

public interface IImportPackageProcessingService
{
    /// <summary>Finds packages in <c>Queued</c>/mid-processing status with an approved plan and
    /// advances each by one checkpointed unit of work (never more than
    /// <paramref name="maxPackages"/> packages per call — the job's own polling interval provides
    /// the loop).</summary>
    Task<IReadOnlyList<ImportPackageProcessingOutcome>> ProcessPendingAsync(int maxPackages, CancellationToken ct = default);
}
