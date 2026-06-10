using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity.Evaluators;

/// <summary>
/// Evaluates read-only / completion-only activities (e.g. lesson_reflection).
/// Always returns completed = true, passed = true, score = 0, maxScore = 0, percentage = 100.
/// No AI call is made.
/// </summary>
public sealed class NoMarkingEvaluator : IPatternEvaluator
{
    public MarkingMode MarkingMode => MarkingMode.NoMarking;

    public Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        // score=0, maxScore=0 → percentage=0 from CalculatePercentage, but spec requires 100
        // We construct manually to express the completion-only semantics.
        var result = new PatternEvaluationResult
        {
            Score = 0,
            MaxScore = 0,
            Percentage = 100,
            Passed = true,
            Completed = true,
            ItemResults = [],
            CoachSummary = "Activity completed.",
            Corrections = [],
            SuggestedImprovedAnswer = null,
            SkillImpacts = [],
            MemorySignals = []
        };

        return Task.FromResult(result);
    }
}
