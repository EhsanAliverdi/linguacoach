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
