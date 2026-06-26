namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Lifecycle status for a StudentActivityReadinessItem.
/// Valid transitions: queued→generating→ready→reserved→consumed
///                                  ↓            ↓
///                               failed        expired
///                    ready→stale, ready→review_only
/// </summary>
public enum ReadinessPoolStatus
{
    Queued = 0,
    Generating = 1,
    Ready = 2,
    Reserved = 3,
    Consumed = 4,
    Expired = 5,
    Failed = 6,
    Stale = 7,
    ReviewOnly = 8,

    /// <summary>
    /// Intentionally bypassed — student has already mastered this objective or it no longer
    /// matches their current level/context and is not useful even for review.
    /// Terminal in the same way as Expired.
    /// </summary>
    Skipped = 9
}
