namespace LinguaCoach.Application.Admin.ReviewQueue;

/// <summary>
/// Phase 9 of the AI bank-first teaching architecture
/// (docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md): a single admin surface
/// listing content across every bank entity that carries an <c>AdminReviewStatus</c>, so an
/// admin doesn't have to visit each entity's own list to find what needs reviewing.
///
/// Scope: <c>ActivityTemplate</c> and <c>PlacementItemDefinition</c> — the two bank-content
/// entities with an ad hoc approve/reject action bar added in earlier phases. Deliberately
/// excludes <c>StudentActivityReadinessItem</c>'s review-scaffold pilot, which is a per-student
/// generated-instance lifecycle with its own dedicated pilot admin surface, not bank curation.
/// </summary>
public static class ReviewQueueEntityType
{
    public const string ActivityTemplate = "ActivityTemplate";
    public const string PlacementItem = "PlacementItem";

    public static readonly IReadOnlyList<string> All = [ActivityTemplate, PlacementItem];
}

public sealed record AdminReviewQueueItemDto(
    string EntityType,
    Guid EntityId,
    /// <summary>Stable identifier — ActivityTemplate.Key, or a derived label for placement items
    /// (which have no natural key).</summary>
    string DisplayKey,
    string Skill,
    string CefrLevel,
    string ReviewStatus,
    DateTime CreatedAt
);

public sealed record ListAdminReviewQueueQuery(
    int Page = 1,
    int PageSize = 20,
    string? EntityType = null,
    string? ReviewStatus = "PendingReview"
);

/// <summary>Items is the current page only, sorted oldest-first (fairest triage order).
/// TotalCount reflects the current filters.</summary>
public sealed record AdminReviewQueueResult(
    IReadOnlyList<AdminReviewQueueItemDto> Items,
    int TotalCount,
    int PendingCount
);

public interface IAdminReviewQueueQuery
{
    Task<AdminReviewQueueResult> HandleAsync(ListAdminReviewQueueQuery query, CancellationToken ct = default);
}
