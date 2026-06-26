namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Represents a student's mastery level for a specific skill or curriculum objective.
/// Computed deterministically from StudentLearningEvent history — no AI required.
/// </summary>
public enum MasteryStatus
{
    /// <summary>Fewer than 3 learning events recorded for this skill/objective.</summary>
    InsufficientEvidence = 0,

    /// <summary>Mixed success/failure, average score 30–79. Needs more practice.</summary>
    NeedsPractice = 1,

    /// <summary>Recent success but average score 50–79. Spaced review recommended.</summary>
    NeedsReview = 2,

    /// <summary>2+ consecutive failures, or average score below 30.</summary>
    AtRisk = 3,

    /// <summary>≥5 events, last 3 consecutive successes, average score ≥80.</summary>
    Mastered = 4
}
