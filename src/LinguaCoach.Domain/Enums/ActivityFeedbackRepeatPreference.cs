namespace LinguaCoach.Domain.Enums;

/// <summary>Student's repeat-content preference signal for a completed activity (Phase B2) —
/// feeds the repetition/novelty foundation described in
/// docs/architecture/repetition-and-novelty.md.</summary>
public enum ActivityFeedbackRepeatPreference
{
    MoreLikeThis,
    NeedRepeat,
    DoNotShowSimilarSoon,
    Neutral,
}
