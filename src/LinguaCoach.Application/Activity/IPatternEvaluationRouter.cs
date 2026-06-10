namespace LinguaCoach.Application.Activity;

public interface IPatternEvaluationRouter
{
    Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken);
}
