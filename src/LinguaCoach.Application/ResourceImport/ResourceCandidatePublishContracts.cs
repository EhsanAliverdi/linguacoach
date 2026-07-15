namespace LinguaCoach.Application.ResourceImport;

// ── Phase E4 — publishes an approved, validated ResourceCandidate into its target Cefr* bank
// table. Distinct from ValidationStatus (deterministic gate, Phase E2) and ReviewStatus (admin
// approve/reject, this phase's IAdminResourceCandidateApproveHandler/RejectHandler) — publish is
// the final step that actually writes a bank row. Idempotent: publishing an already-published
// candidate returns success with the existing bank entity reference, never a second row. ──

/// <summary>
/// Result of one publish attempt. <see cref="Success"/> false means no bank row was written and
/// the candidate's publish state is unchanged — <see cref="Errors"/> lists every reason (a
/// candidate can fail more than one gate at once), never just a generic message.
/// </summary>
public sealed record ResourceCandidatePublishResult(
    bool Success,
    string? PublishedEntityType,
    Guid? PublishedEntityId,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<string> Errors);

public interface IResourceCandidatePublishService
{
    /// <summary>
    /// Publishes one candidate. Re-checks every gate live (language, source approval/license,
    /// ValidationStatus, ReviewStatus) rather than trusting whatever was last stored — a lot can
    /// change between validation/approval time and publish time. Never mutates the candidate's
    /// ResourceRawRecord. Throws <see cref="ResourceImportValidationException"/> only for a
    /// not-found candidate id; every other failure is returned as
    /// <see cref="ResourceCandidatePublishResult.Success"/> = false with explanatory
    /// <see cref="ResourceCandidatePublishResult.Errors"/>, not an exception.
    /// </summary>
    Task<ResourceCandidatePublishResult> PublishAsync(Guid candidateId, Guid? publishedByUserId, CancellationToken ct = default);
}

// ── Phase K2 — batch approve/publish over an explicit set of candidate ids (the current page's
// selection, or a "select all publishable" sweep — see ListAdminResourceCandidatesQuery's
// PublishableOnly filter). Every item is processed independently and continue-on-error, mirroring
// IResourceCandidateBatchAnalysisService's per-row discipline: one candidate's failure never
// aborts the rest of the batch. Never throws for a per-item failure — see
// BatchResourceCandidateActionResult.Items for the per-candidate outcome. ──

public sealed record BatchResourceCandidateActionItemResult(Guid CandidateId, bool Success, string? Error);

public sealed record BatchResourceCandidateActionResult(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    /// <summary>Already-published candidates in the request are treated as a safe no-op, counted
    /// here (not as a failure) and excluded from <see cref="Items"/> beyond a Success=true entry —
    /// mirrors PublishAsync's own idempotent-republish behavior.</summary>
    int AlreadyPublishedCount,
    bool BatchLimitReached,
    IReadOnlyList<BatchResourceCandidateActionItemResult> Items);

public sealed record BatchApproveResourceCandidatesCommand(IReadOnlyList<Guid> CandidateIds, string? Notes = null);
public sealed record BatchPublishResourceCandidatesCommand(IReadOnlyList<Guid> CandidateIds);
public sealed record BatchApproveAndPublishResourceCandidatesCommand(IReadOnlyList<Guid> CandidateIds, string? Notes = null);
public sealed record BatchRejectResourceCandidatesCommand(IReadOnlyList<Guid> CandidateIds, string Reason);
public sealed record BatchSkipResourceCandidatesCommand(IReadOnlyList<Guid> CandidateIds, string? Reason = null);

public interface IResourceCandidateBatchActionService
{
    Task<BatchResourceCandidateActionResult> ApproveAsync(
        BatchApproveResourceCandidatesCommand command, CancellationToken ct = default);

    Task<BatchResourceCandidateActionResult> PublishAsync(
        BatchPublishResourceCandidatesCommand command, Guid? publishedByUserId, CancellationToken ct = default);

    Task<BatchResourceCandidateActionResult> ApproveAndPublishAsync(
        BatchApproveAndPublishResourceCandidatesCommand command, Guid? publishedByUserId, CancellationToken ct = default);

    /// <summary>Phase 3 (2026-07-15 import candidate review workflow).</summary>
    Task<BatchResourceCandidateActionResult> RejectAsync(
        BatchRejectResourceCandidatesCommand command, CancellationToken ct = default);

    /// <summary>Phase 3 (2026-07-15 import candidate review workflow).</summary>
    Task<BatchResourceCandidateActionResult> SkipAsync(
        BatchSkipResourceCandidatesCommand command, CancellationToken ct = default);
}
