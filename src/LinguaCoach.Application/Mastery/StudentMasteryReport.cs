namespace LinguaCoach.Application.Mastery;

/// <summary>
/// Full mastery snapshot for a student, produced by IStudentMasteryEvaluationService.
/// </summary>
public sealed record StudentMasteryReport
{
    public required Guid StudentId { get; init; }
    public required DateTime EvaluatedAtUtc { get; init; }
    public required MasteryEvaluationReason Reason { get; init; }

    public required IReadOnlyList<string> MasteredObjectiveKeys { get; init; }

    /// <summary>
    /// Objectives with NeedsReview signal — sufficient evidence for Completed (not yet Mastered).
    /// A subset of WeakObjectiveKeys.
    /// </summary>
    public required IReadOnlyList<string> CompletedObjectiveKeys { get; init; }

    public required IReadOnlyList<string> WeakObjectiveKeys { get; init; }
    public required IReadOnlyList<string> AtRiskObjectiveKeys { get; init; }

    /// <summary>Always 0 since Phase I2C — the readiness-pool demotion side effect this counted
    /// was removed along with StudentActivityReadinessItem. Kept on the record to avoid churning
    /// every caller; see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.</summary>
    public required int DemotedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int MarkedReviewOnlyCount { get; init; }
}
