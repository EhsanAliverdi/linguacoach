namespace LinguaCoach.Application.Curriculum;

/// <summary>
/// Expresses the caller's intent when requesting curriculum routing.
/// Controls mastery filtering and review scaffold eligibility.
/// </summary>
public enum RoutingMode
{
    /// <summary>
    /// Generate new content the student has not yet mastered.
    /// Mastered objectives are excluded unless no other candidate exists.
    /// </summary>
    NewLearning,

    /// <summary>
    /// Select objectives the student has already seen and needs to revisit.
    /// Includes NeedsReview objectives; optionally includes mastered objectives
    /// when AllowReviewOfMastered is true.
    /// </summary>
    Review,

    /// <summary>
    /// Reinforce objectives where the student is AtRisk or NeedsPractice.
    /// Does not require mastery exclusion but still prefers weak objectives.
    /// </summary>
    Reinforcement,

    /// <summary>
    /// Admin routing preview — shows what would be selected without mutating state.
    /// Mastery exclusion only applied when the preview explicitly requests it.
    /// </summary>
    Preview
}
