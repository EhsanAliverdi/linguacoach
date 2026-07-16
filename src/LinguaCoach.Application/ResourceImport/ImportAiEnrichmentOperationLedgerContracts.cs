using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.4D — durable, retry-safe AI candidate-enrichment operation ledger. Generalizes the
// STT ledger pattern (ImportSttOperationLedgerContracts.cs) — closes TODO-4.4-AI-ENRICHMENT-LEDGER.
// Same documented concurrency boundary as the STT ledger: the unique index on LogicalOperationKey
// guarantees at most one row per logical operation and a Succeeded row is terminal, but a dangling
// Pending row from a crashed pass is treated as safe to re-claim (single-active-worker assumption;
// Quartz clustering remains deferred). ──

/// <summary>Stable identity for one logical AI enrichment operation — content-addressed (package,
/// candidate, candidate content checksum, provider/model, prompt version, processing mode), never
/// a Quartz execution ID or other transient value. Any material change to prompt, model, or
/// processing mode changes the key, so a changed configuration always produces a new operation
/// rather than silently reusing a result computed under different assumptions.</summary>
public static class ImportAiEnrichmentOperationKey
{
    public static string Compute(
        Guid importPackageId, Guid resourceCandidateId, string sourceChecksum,
        string providerName, string? modelName, string promptVersion, string processingMode) =>
        $"{importPackageId:N}:{resourceCandidateId:N}:{sourceChecksum}:{providerName}:{modelName ?? "-"}:{promptVersion}:{processingMode}:ai-enrich";
}

public enum ImportAiClaimOutcome
{
    /// <summary>No existing ledger row, a Failed one being retried, or a Pending one left dangling
    /// by a prior crash — the caller now owns this operation.</summary>
    Claimed,
    /// <summary>An operation with this exact logical key already succeeded — reuse
    /// <see cref="ImportAiEnrichmentOperation.ResultReferenceJson"/> directly; the provider must
    /// not be called again and no cost may be accrued again.</summary>
    AlreadySucceeded,
}

public sealed record ImportAiClaimResult(ImportAiClaimOutcome Outcome, ImportAiEnrichmentOperation Operation);

public interface IImportAiEnrichmentOperationLedger
{
    Task<ImportAiClaimResult> ClaimAsync(
        Guid importPackageId, Guid importProfileId, Guid resourceCandidateId, string logicalOperationKey,
        string operationType, string providerName, string promptVersion, string processingMode,
        CancellationToken ct = default);

    /// <summary>Mutates the ledger row in memory only — does NOT save. The caller must persist
    /// this in the same <c>SaveChangesAsync</c> as the package's cost accrual.</summary>
    Task MarkSucceededAsync(
        ImportAiEnrichmentOperation operation, string resultReferenceJson, decimal calculatedCost, string currency,
        int inputTokens, int outputTokens, decimal inputPricePer1KTokensSnapshot, decimal outputPricePer1KTokensSnapshot,
        string? modelName, CancellationToken ct = default);

    /// <summary>Persists immediately — a failed call accrues no cost.</summary>
    Task MarkFailedAsync(ImportAiEnrichmentOperation operation, string reason, CancellationToken ct = default);
}
