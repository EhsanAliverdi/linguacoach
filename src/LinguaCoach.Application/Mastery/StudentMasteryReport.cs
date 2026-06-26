namespace LinguaCoach.Application.Mastery;

/// <summary>
/// Full mastery snapshot for a student, produced by IStudentMasteryEvaluationService.
/// </summary>
public sealed record StudentMasteryReport
{
    public required Guid StudentId { get; init; }
    public required DateTime EvaluatedAtUtc { get; init; }
    public required MasteryEvaluationReason Reason { get; init; }

    public required IReadOnlyList<string> MasteredObjectiveKeys { get; init; }
    public required IReadOnlyList<string> WeakObjectiveKeys { get; init; }
    public required IReadOnlyList<string> AtRiskObjectiveKeys { get; init; }

    public required int DemotedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int MarkedReviewOnlyCount { get; init; }
}
