namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// Deterministic confidence band for whether a student's weak-event signal justifies
/// generating a review/scaffold item. Derived from existing mastery classification —
/// no AI, no new signal source.
/// </summary>
public enum ReviewNeedConfidence
{
    /// <summary>Weak ledger events exist but the mastery engine has not corroborated them.</summary>
    Low = 0,

    /// <summary>Objective appears in the mastery report's WeakObjectiveKeys (NeedsReview).</summary>
    Medium = 1,

    /// <summary>Objective appears in the mastery report's AtRiskObjectiveKeys (consistent failures).</summary>
    High = 2
}
