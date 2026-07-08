namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Structural/gate validation status of a <see cref="Entities.ResourceCandidate"/>. This is NOT
/// an editorial/admin review status (see <see cref="AdminReviewStatus"/> for that) — it only
/// reflects whether the E1 import gates (language, duplicate, recognizable-content) passed.
/// </summary>
public enum ResourceCandidateValidationStatus
{
    Pending = 0,
    Passed = 1,
    Failed = 2,
    NeedsReview = 3
}
