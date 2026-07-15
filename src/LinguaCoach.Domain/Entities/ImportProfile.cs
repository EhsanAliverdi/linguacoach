using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4 (Part B) — an AI-proposed, administrator-approved mapping/extraction ruleset for one
/// <see cref="ImportPackage"/>, built from analysing a representative sample rather than the full
/// package. Once approved, the full package is processed deterministically against this profile —
/// AI is only re-engaged per-record for enrichment or exception handling, never to re-derive the
/// same schema/mapping decisions on every record. Versioned per package: re-analysing a sample (or
/// an admin edit) creates a new row and marks the prior one <see cref="ImportProfileStatus.Superseded"/>,
/// so approval history is auditable.
/// </summary>
public sealed class ImportProfile : BaseEntity
{
    public Guid ImportPackageId { get; private set; }
    public int Version { get; private set; }
    public ImportProfileStatus Status { get; private set; }

    /// <summary>Phase 4.3 — a serialized <c>List&lt;ImportExecutionGroupInstruction&gt;</c> (see
    /// <c>Application.ResourceImport.ImportExecutionPlanContracts</c>): one frozen, approved
    /// routing/mapping instruction per detected folder group (include/exclude, forced resource
    /// type, column field mappings). This is the actual execution contract — read only through
    /// <c>IApprovedImportProfileResolver</c>, never parsed directly by execution code.</summary>
    public string ProfileJson { get; private set; } = string.Empty;

    public string? AiProviderName { get; private set; }
    public string? AiModelName { get; private set; }
    public IReadOnlyList<Guid> SampleAssetIds { get; private set; } = Array.Empty<Guid>();

    public int EstimatedCandidateCount { get; private set; }

    // ── Mandatory Import Execution Plan addendum (2026-07-15) — every package, regardless of
    // size or processing mode, must have an approved plan with a cost estimate before any
    // material AI/STT/TTS/background processing begins. ──
    public decimal EstimatedCostExpected { get; private set; }
    public decimal EstimatedCostMin { get; private set; }
    public decimal EstimatedCostMax { get; private set; }
    public string Currency { get; private set; } = "USD";
    /// <summary>Volume/time/risk/decision breakdown — see <c>ImportExecutionPlanEstimate</c> for
    /// the deserialized shape. Kept separate from <see cref="ProfileJson"/> (the mapping ruleset)
    /// so the plan's estimate can be regenerated/revised without touching the mapping itself.</summary>
    public string? PlanEstimateJson { get; private set; }
    /// <summary>Snapshot of the per-unit rates used to compute the estimate (AI token price,
    /// STT $/second, TTS $/character, etc.) — so a later pricing change can never silently alter
    /// an already-approved plan's displayed numbers.</summary>
    public string? PricingSnapshotJson { get; private set; }
    /// <summary>Administrator-set spending limit for this plan's execution. Execution must pause
    /// (<see cref="ImportProfileStatus.PausedForCostApproval"/>) before projected cost would
    /// exceed this value.</summary>
    public decimal? ApprovedCostCeiling { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? RejectedAtUtc { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public string? RejectionReason { get; private set; }
    /// <summary>Why this version was created to replace the prior one — required for every
    /// version after the first (Part 7: plan revisions must record a reason).</summary>
    public string? ChangeReason { get; private set; }
    public string? PauseReason { get; private set; }

    private ImportProfile() { }

    public ImportProfile(
        Guid importPackageId,
        int version,
        string profileJson,
        IReadOnlyList<Guid> sampleAssetIds,
        int estimatedCandidateCount,
        DateTimeOffset createdAtUtc,
        string? aiProviderName = null,
        string? aiModelName = null,
        decimal estimatedCostExpected = 0m,
        decimal estimatedCostMin = 0m,
        decimal estimatedCostMax = 0m,
        string currency = "USD",
        string? planEstimateJson = null,
        string? pricingSnapshotJson = null,
        string? changeReason = null)
    {
        if (importPackageId == Guid.Empty)
            throw new ArgumentException("ImportPackageId must not be empty.", nameof(importPackageId));
        if (string.IsNullOrWhiteSpace(profileJson))
            throw new ArgumentException("ProfileJson is required.", nameof(profileJson));
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (version > 1 && string.IsNullOrWhiteSpace(changeReason))
            throw new ArgumentException("ChangeReason is required for every version after the first.", nameof(changeReason));

        ImportPackageId = importPackageId;
        Version = version;
        ProfileJson = profileJson;
        SampleAssetIds = sampleAssetIds ?? Array.Empty<Guid>();
        EstimatedCandidateCount = estimatedCandidateCount;
        CreatedAtUtc = createdAtUtc;
        AiProviderName = aiProviderName;
        AiModelName = aiModelName;
        EstimatedCostExpected = estimatedCostExpected;
        EstimatedCostMin = estimatedCostMin;
        EstimatedCostMax = estimatedCostMax;
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency;
        PlanEstimateJson = planEstimateJson;
        PricingSnapshotJson = pricingSnapshotJson;
        ChangeReason = changeReason?.Trim();
        Status = ImportProfileStatus.Draft;
    }

    /// <summary>Admin edits/corrections to the AI-proposed profile are applied here before
    /// approval — the profile is editable, per the requirement, without needing a whole new AI
    /// analysis pass just to fix one mapping rule.</summary>
    public void ReplaceProfileJson(string profileJson)
    {
        if (Status != ImportProfileStatus.Draft)
            throw new InvalidOperationException("Only a Draft Import Profile can be edited — approve creates an audit boundary.");
        if (string.IsNullOrWhiteSpace(profileJson))
            throw new ArgumentException("ProfileJson is required.", nameof(profileJson));

        ProfileJson = profileJson;
    }

    /// <summary>Draft -> AwaitingApproval. Plan generation calls this once the estimate is
    /// computed — nothing may auto-advance past this point.</summary>
    public void SubmitForApproval()
    {
        if (Status != ImportProfileStatus.Draft)
            throw new InvalidOperationException($"Cannot submit an Import Plan in status '{Status}' for approval.");

        Status = ImportProfileStatus.AwaitingApproval;
    }

    /// <summary>The one and only path into <see cref="ImportProfileStatus.Approved"/> — always an
    /// explicit administrator action ("Approve and Start Processing"), never a side effect of
    /// upload or plan generation. <paramref name="approvedCostCeiling"/> is required: every
    /// approved plan carries a spending limit, even a modest/free one.</summary>
    public void Approve(Guid? approvedByUserId, DateTimeOffset approvedAtUtc, decimal approvedCostCeiling)
    {
        if (Status != ImportProfileStatus.AwaitingApproval)
            throw new InvalidOperationException($"Cannot approve an Import Plan in status '{Status}'.");
        if (approvedCostCeiling < 0)
            throw new ArgumentOutOfRangeException(nameof(approvedCostCeiling), "Approved cost ceiling cannot be negative.");

        Status = ImportProfileStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAtUtc = approvedAtUtc;
        ApprovedCostCeiling = approvedCostCeiling;
    }

    public void Reject(Guid? rejectedByUserId, DateTimeOffset rejectedAtUtc, string reason)
    {
        if (Status != ImportProfileStatus.AwaitingApproval)
            throw new InvalidOperationException($"Cannot reject an Import Plan in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection reason is required.", nameof(reason));

        Status = ImportProfileStatus.Rejected;
        RejectedByUserId = rejectedByUserId;
        RejectedAtUtc = rejectedAtUtc;
        RejectionReason = reason.Trim();
    }

    public void MarkExecuting()
    {
        if (Status is not (ImportProfileStatus.Approved or ImportProfileStatus.PausedForCostApproval))
            throw new InvalidOperationException($"Cannot start executing an Import Plan in status '{Status}'.");

        Status = ImportProfileStatus.Executing;
        PauseReason = null;
    }

    /// <summary>Part 6 — the execution job must pause, not silently continue, before projected
    /// cost would exceed <see cref="ApprovedCostCeiling"/>.</summary>
    public void PauseForCostApproval(string reason)
    {
        if (Status != ImportProfileStatus.Executing)
            throw new InvalidOperationException($"Cannot pause an Import Plan in status '{Status}' for cost approval.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A pause reason is required.", nameof(reason));

        Status = ImportProfileStatus.PausedForCostApproval;
        PauseReason = reason.Trim();
    }

    /// <summary>Raises the ceiling and resumes a paused plan without creating a new version —
    /// used when the administrator simply approves the higher cost rather than revising the plan
    /// itself.</summary>
    public void ApproveRevisedCeilingAndResume(decimal newApprovedCostCeiling)
    {
        if (Status != ImportProfileStatus.PausedForCostApproval)
            throw new InvalidOperationException($"Cannot resume an Import Plan in status '{Status}'.");
        if (newApprovedCostCeiling < 0)
            throw new ArgumentOutOfRangeException(nameof(newApprovedCostCeiling));

        ApprovedCostCeiling = newApprovedCostCeiling;
        Status = ImportProfileStatus.Executing;
        PauseReason = null;
    }

    public void MarkCompleted()
    {
        if (Status != ImportProfileStatus.Executing)
            throw new InvalidOperationException($"Cannot complete an Import Plan in status '{Status}'.");

        Status = ImportProfileStatus.Completed;
    }

    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A failure reason is required.", nameof(reason));

        Status = ImportProfileStatus.Failed;
        PauseReason = reason.Trim();
    }

    public void Cancel()
    {
        if (Status is ImportProfileStatus.Completed or ImportProfileStatus.Failed or ImportProfileStatus.Superseded)
            throw new InvalidOperationException($"Cannot cancel an Import Plan in status '{Status}'.");

        Status = ImportProfileStatus.Cancelled;
    }

    public void Supersede()
    {
        if (Status is ImportProfileStatus.Completed or ImportProfileStatus.Executing)
            throw new InvalidOperationException($"Cannot supersede an Import Plan in status '{Status}'.");

        Status = ImportProfileStatus.Superseded;
    }
}
