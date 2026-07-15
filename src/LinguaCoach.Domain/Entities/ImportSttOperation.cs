using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4.4 — one durable, retry-safe STT (speech-to-text) billable operation record: the
/// "highest-risk duplicate-cost path" identified for Import processing (an audio file with no
/// matching transcript requires a real, billable provider call to transcribe). Exactly one row
/// exists per <see cref="LogicalOperationKey"/> (enforced by a unique database index — see
/// <c>ImportSttOperationConfiguration</c>), mutated in place across retries rather than
/// accumulating a new row per attempt, so "has this exact operation already succeeded?" is always
/// a single-row lookup. A successful result's <see cref="TranscriptText"/> and
/// <see cref="CalculatedCost"/> are reused on retry — the provider is never called again and the
/// package's accrued cost is never incremented twice for the same logical operation.
/// </summary>
public sealed class ImportSttOperation : BaseEntity
{
    public Guid ImportPackageId { get; private set; }
    public Guid ImportProfileId { get; private set; }
    public Guid ImportAssetId { get; private set; }

    /// <summary>Stable identity derived from materially relevant inputs only (package, asset,
    /// asset content checksum) — never from a Quartz execution ID or other transient/in-memory
    /// value. Transcription output depends only on the audio bytes, not on the plan's routing/
    /// mapping choices, so the key intentionally excludes the profile revision (see the type-level
    /// doc comment on why <see cref="ImportProfileId"/> is still stored, for provenance, without
    /// being part of the key). If the asset's checksum changes (re-uploaded/replaced content), the
    /// key changes too — a stale measurement/result is never reused across different content.
    /// </summary>
    public string LogicalOperationKey { get; private set; } = string.Empty;

    public ImportSttOperationStatus Status { get; private set; }
    public int AttemptNumber { get; private set; }

    public string ProviderName { get; private set; } = string.Empty;
    public string? ModelName { get; private set; }

    /// <summary>Set only once, on success — never overwritten by a later failed retry (there is
    /// none: a Succeeded operation can never transition again, see <see cref="MarkSucceeded"/>).
    /// </summary>
    public string? TranscriptText { get; private set; }

    // ── Pricing snapshot (Phase 4.4, B8) — the exact rate applied to this specific operation,
    // immutable once set, so a later change to ImportCostEstimationOptions can never retroactively
    // alter what this historical operation is recorded as having cost. ──
    public decimal AssumedMinutes { get; private set; }
    public decimal? PricePerMinuteSnapshot { get; private set; }
    public decimal? CalculatedCost { get; private set; }
    public string Currency { get; private set; } = "USD";

    public string? FailureReason { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private ImportSttOperation() { }

    public ImportSttOperation(
        Guid importPackageId, Guid importProfileId, Guid importAssetId, string logicalOperationKey,
        string providerName, decimal assumedMinutes, DateTimeOffset startedAtUtc)
    {
        if (importPackageId == Guid.Empty)
            throw new ArgumentException("ImportPackageId must not be empty.", nameof(importPackageId));
        if (importAssetId == Guid.Empty)
            throw new ArgumentException("ImportAssetId must not be empty.", nameof(importAssetId));
        if (string.IsNullOrWhiteSpace(logicalOperationKey))
            throw new ArgumentException("LogicalOperationKey is required.", nameof(logicalOperationKey));
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("ProviderName is required.", nameof(providerName));
        if (assumedMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(assumedMinutes));

        ImportPackageId = importPackageId;
        ImportProfileId = importProfileId;
        ImportAssetId = importAssetId;
        LogicalOperationKey = logicalOperationKey.Trim();
        ProviderName = providerName.Trim();
        AssumedMinutes = assumedMinutes;
        StartedAtUtc = startedAtUtc;
        Status = ImportSttOperationStatus.Pending;
        AttemptNumber = 1;
    }

    public void MarkSucceeded(
        string transcriptText, decimal calculatedCost, string currency, decimal pricePerMinuteSnapshot,
        string? modelName, DateTimeOffset completedAtUtc)
    {
        if (Status != ImportSttOperationStatus.Pending)
            throw new InvalidOperationException($"Cannot mark an STT operation succeeded from status '{Status}'.");
        if (calculatedCost < 0)
            throw new ArgumentOutOfRangeException(nameof(calculatedCost));

        Status = ImportSttOperationStatus.Succeeded;
        TranscriptText = transcriptText;
        CalculatedCost = calculatedCost;
        Currency = currency;
        PricePerMinuteSnapshot = pricePerMinuteSnapshot;
        ModelName = modelName;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkFailed(string reason, DateTimeOffset completedAtUtc)
    {
        if (Status != ImportSttOperationStatus.Pending)
            throw new InvalidOperationException($"Cannot mark an STT operation failed from status '{Status}'.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A failure reason is required.", nameof(reason));

        Status = ImportSttOperationStatus.Failed;
        FailureReason = reason.Trim();
        CompletedAtUtc = completedAtUtc;
    }

    /// <summary>A prior attempt failed — allow exactly one more attempt under the same logical
    /// key, per the documented retry policy (no unbounded retry loop; the caller decides whether
    /// to call this based on <see cref="AttemptNumber"/>). Never callable from Succeeded — a
    /// completed, billed result is never re-attempted.</summary>
    public void BeginRetry(DateTimeOffset startedAtUtc)
    {
        if (Status != ImportSttOperationStatus.Failed)
            throw new InvalidOperationException($"Cannot retry an STT operation from status '{Status}' — only a Failed operation may retry.");

        Status = ImportSttOperationStatus.Pending;
        AttemptNumber++;
        FailureReason = null;
        CompletedAtUtc = null;
        StartedAtUtc = startedAtUtc;
    }
}
