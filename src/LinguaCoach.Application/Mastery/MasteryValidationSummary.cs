namespace LinguaCoach.Application.Mastery;

/// <summary>
/// Aggregate diagnostic snapshot of mastery signal quality across all active students.
/// Produced on demand by admin — no AI calls, pure rule-based aggregate.
/// </summary>
public sealed record MasteryValidationSummary
{
    public int TotalStudentsEvaluated { get; init; }
    public int TotalObjectivesEvaluated { get; init; }

    // Status breakdown across all student × objective pairs.
    public int CountInsufficientEvidence { get; init; }
    public int CountMastered { get; init; }
    public int CountNeedsReview { get; init; }
    public int CountNeedsPractice { get; init; }
    public int CountAtRisk { get; init; }

    // Mastered objectives currently excluded from new-learning routing.
    public int MasteredExcludedFromNewLearning { get; init; }

    // Suspicious-pattern warnings. Non-empty indicates signal quality concerns.
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public DateTime GeneratedAt { get; init; }
}
