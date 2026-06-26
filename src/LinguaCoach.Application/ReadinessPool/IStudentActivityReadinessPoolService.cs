using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// Manages the student activity readiness pool lifecycle.
/// All mutation methods enforce valid state transitions and are safe against
/// concurrent callers via optimistic concurrency (xmin).
/// </summary>
public interface IStudentActivityReadinessPoolService
{
    /// <summary>Creates a new queued item with a routing snapshot. Returns the new item's Id.</summary>
    Task<Guid> CreateQueuedAsync(CreateReadinessItemRequest request, CancellationToken ct = default);

    /// <summary>queued → generating. Increments AttemptCount.</summary>
    Task MarkGeneratingAsync(Guid itemId, CancellationToken ct = default);

    /// <summary>generating → ready. Optionally links materialized entity ids.</summary>
    Task MarkReadyAsync(
        Guid itemId,
        Guid? learningSessionId = null,
        Guid? learningActivityId = null,
        Guid? sessionExerciseId = null,
        CancellationToken ct = default);

    /// <summary>generating → failed.</summary>
    Task MarkFailedAsync(Guid itemId, string? errorCode, string? errorMessage, CancellationToken ct = default);

    /// <summary>
    /// Reserves the next ready item for a student/source. Safe against double-reservation.
    /// Returns null when no suitable item is available.
    /// </summary>
    Task<StudentActivityReadinessItem?> ReserveNextReadyAsync(
        Guid studentId,
        ReadinessPoolSource source,
        string? patternKey = null,
        string? primarySkill = null,
        CancellationToken ct = default);

    /// <summary>reserved → consumed. Terminal.</summary>
    Task MarkConsumedAsync(Guid itemId, CancellationToken ct = default);

    /// <summary>Any non-terminal → expired.</summary>
    Task ExpireAsync(Guid itemId, string? reason = null, CancellationToken ct = default);

    /// <summary>ready/reserved → stale (routing/mastery no longer matches).</summary>
    Task MarkStaleAsync(Guid itemId, string? reason = null, CancellationToken ct = default);

    /// <summary>ready/reserved → review_only (content still useful only as review).</summary>
    Task MarkReviewOnlyAsync(Guid itemId, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Any non-terminal → skipped. Student has mastered the objective or it is no longer
    /// relevant even for review. Terminal (same as Expired).
    /// </summary>
    Task MarkSkippedAsync(Guid itemId, string? reason = null, CancellationToken ct = default);

    /// <summary>Links materialized entity ids to an existing item (any non-terminal status).</summary>
    Task LinkMaterializedIdsAsync(
        Guid itemId,
        Guid? learningSessionId,
        Guid? learningActivityId,
        Guid? sessionExerciseId,
        CancellationToken ct = default);

    /// <summary>Returns ready items for a student (excludes stale/review_only/expired/failed).</summary>
    Task<IReadOnlyList<StudentActivityReadinessItem>> GetReadyForStudentAsync(
        Guid studentId,
        ReadinessPoolSource? source = null,
        CancellationToken ct = default);

    /// <summary>Pool summary with counts by status and item list for admin inspection.</summary>
    Task<ReadinessPoolSummary> GetPoolSummaryAsync(Guid studentId, CancellationToken ct = default);
}
