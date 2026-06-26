namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Decision produced by the mastery engine when evaluating whether a readiness pool item
/// should be demoted, kept, or transitioned to a different lifecycle state.
/// </summary>
public enum ReadinessDemotionDecision
{
    /// <summary>No state change needed; item is appropriate for normal serving.</summary>
    KeepReady = 0,

    /// <summary>Objective is mastered but the item still has spaced-review value. Transition to ReviewOnly.</summary>
    ConvertToReviewOnly = 1,

    /// <summary>Objective is mastered and item has no review value. Mark Skipped.</summary>
    Skip = 2,

    /// <summary>Item CEFR level no longer matches student level (mismatch > 1 level). Mark Stale.</summary>
    MarkStale = 3,

    /// <summary>Item is old (> 90 days) and was never consumed. Mark Expired.</summary>
    Expire = 4,

    /// <summary>Item is already in a terminal state (Consumed, Expired, Failed, Skipped). No action.</summary>
    NoChange = 5
}
