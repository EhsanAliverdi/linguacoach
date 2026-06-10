using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

public interface IPatternEvaluator
{
    MarkingMode MarkingMode { get; }

    Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken);
}
