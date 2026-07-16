using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4 (2026-07-15 large-scale AI import packages) — one logical Import Package: a ZIP
/// archive, a set of individually-uploaded related files, or pasted content plus supporting
/// files, scoped to one <see cref="CefrResourceSource"/>. Owns zero or more
/// <see cref="ImportAsset"/> rows (the file inventory) and, once processed, zero or more
/// <see cref="ResourceImportRun"/> rows (one per detected schema/file group — reuses the entire
/// existing Phase E1-K2/Phase-3 candidate staging/review/publish pipeline unchanged; this entity
/// is purely an upstream staging concept that feeds it).
///
/// Never itself writes to a published Cefr* bank table.
/// </summary>
public sealed class ImportPackage : BaseEntity
{
    public Guid CefrResourceSourceId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    public string OriginalArchiveFileName { get; private set; } = string.Empty;
    /// <summary>Null for a paste/multi-file (non-ZIP) package.</summary>
    public string? ArchiveStorageKey { get; private set; }
    public string? ArchiveChecksum { get; private set; }
    public long? CompressedSizeBytes { get; private set; }

    public ImportPackageStatus Status { get; private set; }
    public ImportProcessingMode? ProcessingMode { get; private set; }
    public string? ProcessingModeReason { get; private set; }

    /// <summary>The package manifest (Part B) — file inventory, structure, candidate schemas,
    /// sample recommendations. See <c>ImportPackageManifest</c> for the deserialized shape.</summary>
    public string? ManifestJson { get; private set; }

    public Guid? ApprovedImportProfileId { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? ErrorSummary { get; private set; }
    public string? Notes { get; private set; }

    // ── Part I — stage-based progress (never fake precision; whole-number counts only) ────────
    public int FilesInspectedCount { get; private set; }
    public int FilesProcessedCount { get; private set; }
    public int RecordsProcessedCount { get; private set; }
    public int CandidatesCreatedCount { get; private set; }
    public int CandidatesFailedCount { get; private set; }

    /// <summary>Idempotency/checkpoint marker — the last successfully completed pipeline stage
    /// index, so a retried/resumed job skips already-completed stages rather than restarting from
    /// the first file. See <c>ImportPackageProcessingStage</c> for the ordered stage list.</summary>
    public int LastCompletedStageIndex { get; private set; } = -1;

    /// <summary>Phase 4.4 — durable, persisted running total of calculated cost for billable
    /// operations completed so far (see <c>ImportSttOperation</c> and any future operation ledger
    /// entries). Unlike the pre-4.4 in-memory <c>runningCost</c> local variable in
    /// <c>ImportPackageProcessingService</c> (which reset to zero on every processing pass), this
    /// survives a process/job restart — a retry after a crash mid-package must not lose track of
    /// what was already spent, and the cost-ceiling check must compare against this value, not an
    /// ephemeral one.</summary>
    public decimal AccruedCost { get; private set; }
    public string AccruedCostCurrency { get; private set; } = "USD";

    // ── Phase 4.8 (2026-07-17 security/concurrency/idempotency) — durable claim/lease so two
    // background workers (or two nodes, if ever run without Quartz clustering) cannot process the
    // same package at once. Backed by a database-level optimistic concurrency check on
    // <see cref="ConcurrencyStamp"/> (see <c>ImportPackageConfiguration</c>) — a worker's claim
    // attempt is a normal EF SaveChanges that fails with a concurrency exception if another worker
    // committed a claim first, never a race two readers can both win. ──
    public string? ClaimedByWorkerId { get; private set; }
    public DateTimeOffset? ClaimedAtUtc { get; private set; }
    public DateTimeOffset? ClaimExpiresAtUtc { get; private set; }

    /// <summary>Database-level optimistic concurrency token (see
    /// <c>ImportPackageConfiguration.Configure</c>'s <c>IsConcurrencyToken()</c>) — every claim,
    /// renewal, or release regenerates this, so a concurrent writer's SaveChanges against a stale
    /// value fails atomically instead of silently overwriting the claim.</summary>
    public Guid ConcurrencyStamp { get; private set; }

    private ImportPackage() { }

    public ImportPackage(
        Guid cefrResourceSourceId,
        string originalArchiveFileName,
        DateTimeOffset startedAtUtc,
        Guid? createdByUserId = null,
        string? archiveStorageKey = null,
        string? archiveChecksum = null,
        long? compressedSizeBytes = null,
        string? notes = null)
    {
        if (cefrResourceSourceId == Guid.Empty)
            throw new ArgumentException("CefrResourceSourceId must not be empty.", nameof(cefrResourceSourceId));
        if (string.IsNullOrWhiteSpace(originalArchiveFileName))
            throw new ArgumentException("OriginalArchiveFileName is required.", nameof(originalArchiveFileName));

        CefrResourceSourceId = cefrResourceSourceId;
        OriginalArchiveFileName = originalArchiveFileName.Trim();
        StartedAtUtc = startedAtUtc;
        CreatedByUserId = createdByUserId;
        ArchiveStorageKey = archiveStorageKey;
        ArchiveChecksum = archiveChecksum;
        CompressedSizeBytes = compressedSizeBytes;
        Notes = notes?.Trim();
        Status = ImportPackageStatus.Uploaded;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void SetManifest(string manifestJson, int filesInspectedCount)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            throw new ArgumentException("ManifestJson is required.", nameof(manifestJson));

        ManifestJson = manifestJson;
        FilesInspectedCount = filesInspectedCount;
        Status = ImportPackageStatus.Uploaded == Status || Status == ImportPackageStatus.InspectingPackage
            ? ImportPackageStatus.AwaitingSample
            : Status;
    }

    /// <summary>Phase 4.8 — records the whole-archive checksum at inspection time for a package
    /// whose upload path didn't already know it (the legacy single-shot upload flow never
    /// computed one before this phase; the Phase 4.7 chunked-upload session flow already sets it
    /// via the constructor since it computes the checksum while assembling the archive). This is
    /// the value <see cref="ImportPackageProcessingService"/> revalidates the live archive against
    /// before ever extracting from it.</summary>
    public void SetArchiveChecksum(string checksum)
    {
        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Checksum is required.", nameof(checksum));

        ArchiveChecksum = checksum.Trim();
    }

    public void SetProcessingMode(ImportProcessingMode mode, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        ProcessingMode = mode;
        ProcessingModeReason = reason.Trim();
        Status = mode == ImportProcessingMode.SampleDriven
            ? ImportPackageStatus.AwaitingSample
            : ImportPackageStatus.Queued;
    }

    public void MoveToStatus(ImportPackageStatus status)
    {
        Status = status;
    }

    public void ApproveProfile(Guid importProfileId)
    {
        if (importProfileId == Guid.Empty)
            throw new ArgumentException("ImportProfileId must not be empty.", nameof(importProfileId));

        ApprovedImportProfileId = importProfileId;
        Status = ImportPackageStatus.Queued;
    }

    /// <summary>Records that <paramref name="stageIndex"/> completed successfully — a resumed job
    /// re-checks this before repeating a stage, satisfying "do not restart processing from the
    /// first file after a recoverable failure."</summary>
    public void CheckpointStage(int stageIndex)
    {
        if (stageIndex > LastCompletedStageIndex)
            LastCompletedStageIndex = stageIndex;
    }

    public void UpdateProgress(
        int? filesProcessedCount = null,
        int? recordsProcessedCount = null,
        int? candidatesCreatedCount = null,
        int? candidatesFailedCount = null)
    {
        if (filesProcessedCount is not null) FilesProcessedCount = filesProcessedCount.Value;
        if (recordsProcessedCount is not null) RecordsProcessedCount = recordsProcessedCount.Value;
        if (candidatesCreatedCount is not null) CandidatesCreatedCount = candidatesCreatedCount.Value;
        if (candidatesFailedCount is not null) CandidatesFailedCount = candidatesFailedCount.Value;
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        CompletedAtUtc = completedAtUtc;
        Status = CandidatesFailedCount > 0
            ? ImportPackageStatus.CompletedWithWarnings
            : ImportPackageStatus.ReadyForReview;
    }

    public void MarkFailed(string errorSummary, DateTimeOffset completedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(errorSummary))
            throw new ArgumentException("ErrorSummary is required.", nameof(errorSummary));

        Status = ImportPackageStatus.Failed;
        ErrorSummary = errorSummary.Trim();
        CompletedAtUtc = completedAtUtc;
    }

    public void Cancel()
    {
        Status = ImportPackageStatus.Cancelled;
    }

    /// <summary>Phase 4.4 — records calculated cost for one completed billable operation against
    /// the package's durable running total. Must be called in the same <c>SaveChangesAsync</c> as
    /// the operation ledger row that produced <paramref name="amount"/>, so the two never drift.
    /// Never negative — a reused/no-charge operation must call this with <c>0m</c> or not at all,
    /// never with a negative "refund".</summary>
    public void AccrueCost(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Accrued cost cannot be reduced by a negative amount.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));
        if (AccruedCost > 0 && !string.Equals(AccruedCostCurrency, currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cannot accrue cost in '{currency}' — this package already has accrued cost in '{AccruedCostCurrency}'.");

        AccruedCost += amount;
        AccruedCostCurrency = currency;
    }

    /// <summary>True when no worker currently holds a live lease — either never claimed, or the
    /// prior claim's lease has expired (a crashed worker never releases explicitly, so expiry is
    /// the only recovery path for that case).</summary>
    public bool IsClaimable(DateTimeOffset nowUtc)
        => ClaimedByWorkerId is null || ClaimExpiresAtUtc is null || nowUtc > ClaimExpiresAtUtc.Value;

    /// <summary>Acquires the processing lease. Throws if another worker currently holds a live
    /// lease — callers must check <see cref="IsClaimable"/> first; the real race protection is the
    /// database-level concurrency check on the following <c>SaveChangesAsync</c>, not this
    /// in-memory guard (which only protects against a caller misusing its own already-loaded,
    /// possibly-stale instance).</summary>
    public void Claim(string workerId, DateTimeOffset nowUtc, TimeSpan leaseDuration)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("WorkerId is required.", nameof(workerId));
        if (!IsClaimable(nowUtc))
            throw new InvalidOperationException(
                $"Package '{Id}' is already claimed by worker '{ClaimedByWorkerId}' until {ClaimExpiresAtUtc:O}.");

        ClaimedByWorkerId = workerId.Trim();
        ClaimedAtUtc = nowUtc;
        ClaimExpiresAtUtc = nowUtc.Add(leaseDuration);
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>Extends the lease held by the same worker — used for a long-running processing
    /// pass so the lease does not expire mid-work and invite a second worker to steal it.</summary>
    public void RenewClaim(string workerId, DateTimeOffset nowUtc, TimeSpan leaseDuration)
    {
        if (!string.Equals(ClaimedByWorkerId, workerId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Package '{Id}' is not currently claimed by worker '{workerId}'.");

        ClaimExpiresAtUtc = nowUtc.Add(leaseDuration);
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>Releases the lease so the package is immediately claimable again — called after a
    /// processing pass whether it succeeded, paused, or failed, so a package that legitimately
    /// remains in a claimable status (still <c>Extracting</c>/<c>Mapping</c>/etc. because more
    /// checkpointed work remains) does not have to wait out the full lease duration before the
    /// next pass can pick it up.</summary>
    public void ReleaseClaim()
    {
        ClaimedByWorkerId = null;
        ClaimedAtUtc = null;
        ClaimExpiresAtUtc = null;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
