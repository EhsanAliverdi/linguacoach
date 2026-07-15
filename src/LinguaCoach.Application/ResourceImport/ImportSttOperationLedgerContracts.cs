using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.4 (Workstream B3/B4) — the durable, retry-safe STT operation ledger. See
// Domain.Entities.ImportSttOperation for the persisted row shape and its unique-index-backed
// dedup guarantee. This is the "highest-risk duplicate-cost path" the phase brief calls out —
// scoped deliberately to STT for this phase (an AI-enrichment ledger is deferred, see
// TODO-4.4-AI-ENRICHMENT-LEDGER).
//
// Documented remaining concurrency boundary (Workstream B11): the unique database index on
// LogicalOperationKey guarantees at most one row per logical operation, and a Succeeded row is
// terminal (never mutated again) — so two callers can never both record a successful charge for
// the same content. What is NOT guaranteed is strict mutual exclusion of the in-between window: a
// dangling Pending row (left by a prior pass that crashed after claiming but before recording an
// outcome) is treated as safe to re-claim rather than a permanent block, which assumes at most one
// active package-processing worker at a time — the same single-worker assumption the rest of this
// codebase's Import pipeline already makes (Quartz clustering remains deferred). A genuinely
// concurrent second worker racing the exact same claim would only be caught by the unique index at
// INSERT time (a brand-new logical key); it is not caught for a re-claimed Pending/Failed row. ──

/// <summary>Stable identity for one logical STT operation — content-addressed (package + asset +
/// checksum), never a Quartz execution ID or other transient value, so a retry of the exact same
/// audio content always resolves to the exact same ledger row.</summary>
public static class ImportSttOperationKey
{
    public static string Compute(Guid importPackageId, Guid importAssetId, string checksum) =>
        $"{importPackageId:N}:{importAssetId:N}:{checksum}:stt-transcribe";
}

public enum ImportSttClaimOutcome
{
    /// <summary>No existing ledger row, a Failed one being retried, or a Pending one left dangling
    /// by a prior crash (see the type-level doc comment for why a dangling Pending row is safe to
    /// re-enter rather than permanently blocking) — the caller now owns this operation and must
    /// call the provider, then <see cref="IImportSttOperationLedger.MarkSucceededAsync"/> or
    /// <see cref="IImportSttOperationLedger.MarkFailedAsync"/>.</summary>
    Claimed,
    /// <summary>An operation with this exact logical key already succeeded — reuse
    /// <see cref="ImportSttOperation.TranscriptText"/> directly; the provider must not be called
    /// again and no cost may be accrued again.</summary>
    AlreadySucceeded,
}

public sealed record ImportSttClaimResult(ImportSttClaimOutcome Outcome, ImportSttOperation Operation);

public interface IImportSttOperationLedger
{
    /// <summary>Atomically claims (or reuses, or detects an in-flight collision for) one logical
    /// STT operation. See <see cref="ImportSttClaimOutcome"/> for what each result means the
    /// caller must (or must not) do next.</summary>
    Task<ImportSttClaimResult> ClaimAsync(
        Guid importPackageId, Guid importProfileId, Guid importAssetId, string logicalOperationKey,
        string providerName, decimal assumedMinutes, CancellationToken ct = default);

    /// <summary>Mutates the ledger row in memory only — does NOT save. The caller must persist
    /// this in the same <c>SaveChangesAsync</c> as the package's cost accrual, so the ledger row
    /// and the durable running total can never drift apart from a crash between two separate
    /// saves.</summary>
    Task MarkSucceededAsync(
        ImportSttOperation operation, string transcriptText, decimal calculatedCost, string currency,
        decimal pricePerMinuteSnapshot, string? modelName, CancellationToken ct = default);

    /// <summary>Persists immediately — a failed call accrues no cost, so there is nothing else to
    /// combine this save with.</summary>
    Task MarkFailedAsync(ImportSttOperation operation, string reason, CancellationToken ct = default);
}
