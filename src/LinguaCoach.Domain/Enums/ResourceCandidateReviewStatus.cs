namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Phase 3 (2026-07-15 import candidate review workflow) — the explicit admin review decision for
/// a <see cref="Entities.ResourceCandidate"/>. Previously reused the shared
/// <see cref="AdminReviewStatus"/> enum (also used by Module/Lesson/Exercise); split into its own
/// type instead of adding a candidate-only <c>Skipped</c> member to that shared enum, since
/// Skipped has no meaning for Module/Lesson/Exercise review. Existing stored string values
/// ("NotRequired"/"PendingReview"/"Approved"/"Rejected") are unaffected — the column is a
/// string-converted varchar, not a DB-level enum type, and every member name is unchanged.
/// </summary>
public enum ResourceCandidateReviewStatus
{
    /// <summary>Not yet past the deterministic validation gate — never shown in the review queue.</summary>
    NotRequired = 0,

    /// <summary>Validation passed or passed-with-warnings; awaiting an explicit admin decision.</summary>
    PendingReview = 1,

    Approved = 2,

    Rejected = 3,

    /// <summary>Phase 3 — "I am intentionally ignoring this candidate," distinct from never having
    /// been reviewed at all (<see cref="PendingReview"/>). A skipped candidate stays in the Import
    /// Run, can be re-reviewed later (Approve/Reject/back to PendingReview are all still reachable),
    /// and — like Rejected — can never be published while in this state.</summary>
    Skipped = 4
}
