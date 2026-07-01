namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Per-item admin approval state for review scaffold readiness items (Phase 19B).
/// Only meaningful when RequiresAdminReview=true was set at creation time.
/// Valid transitions: PendingReview→Approved, PendingReview→Rejected,
/// Approved→Rejected (only if not consumed), Rejected→PendingReview (explicit reopen only).
/// </summary>
public enum AdminReviewStatus
{
    /// <summary>Item was not generated under RequireAdminReview=true; no approval needed.</summary>
    NotRequired = 0,

    /// <summary>Held from students until an admin approves or rejects it.</summary>
    PendingReview = 1,

    /// <summary>Admin-approved; may become student/Practice-Gym visible if other lifecycle gates pass.</summary>
    Approved = 2,

    /// <summary>Admin-rejected; permanently hidden from students unless explicitly reopened.</summary>
    Rejected = 3
}
