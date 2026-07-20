namespace LinguaCoach.Application.ResourceImport;

/// <summary>
/// Platform Reliability Sprint 8 — one-time repair for a historical data-integrity gap: the
/// Phase I0 "drop typed bank tables" migration dropped `CefrGrammarProfileEntry`/
/// `CefrReadingReference`/`CefrReadingPassage` without migrating the `ResourceCandidate` rows that
/// had already published into them, permanently orphaning their publish reference (`IsPublished`
/// stayed true, but `PublishedEntityId` points at a table that no longer exists). Since
/// `IsPublished` blocks editing/re-publishing/rejecting and this codebase has no unpublish step,
/// these candidates were stuck — real, approved content that could never reach the Resource Bank.
/// This service repairs the publish reference (see
/// <c>ResourceCandidate.RepairOrphanedPublishReference</c>) then re-publishes each one through the
/// real, fully-gated <see cref="IResourceCandidatePublishService"/> pipeline — no shortcuts, every
/// gate (English-only, source approval/license, validation status, review status) still applies.
/// </summary>
public interface IResourceCandidateOrphanRepairService
{
    Task<OrphanedPublishRepairResult> RepairOrphanedPublishReferencesAsync(CancellationToken ct = default);
}

public sealed record OrphanedPublishRepairItemResult(
    Guid CandidateId, string CandidateType, bool Repaired, Guid? NewBankItemId, string? Error);

public sealed record OrphanedPublishRepairResult(
    int FoundCount,
    int RepairedCount,
    int FailedCount,
    IReadOnlyList<OrphanedPublishRepairItemResult> Items);
