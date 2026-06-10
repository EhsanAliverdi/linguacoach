using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Routes a PatternEvaluationRequest to the correct IPatternEvaluator by MarkingMode.
/// AI-marked modes (AiStructured, AiOpenEnded) are not yet implemented and return a
/// controlled not-implemented result so existing submission flow is not blocked.
/// </summary>
public sealed class PatternEvaluationRouter : IPatternEvaluationRouter
{
    private readonly IReadOnlyDictionary<MarkingMode, IPatternEvaluator> _evaluators;
    private readonly ILogger<PatternEvaluationRouter> _logger;

    public PatternEvaluationRouter(
        IEnumerable<IPatternEvaluator> evaluators,
        ILogger<PatternEvaluationRouter> logger)
    {
        _evaluators = evaluators.ToDictionary(e => e.MarkingMode);
        _logger = logger;
    }

    public Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (_evaluators.TryGetValue(request.MarkingMode, out var evaluator))
        {
            _logger.LogDebug(
                "PatternEvaluationRouter dispatching ActivityId={ActivityId} PatternKey={PatternKey} MarkingMode={MarkingMode}",
                request.ActivityId, request.ExercisePatternKey, request.MarkingMode);

            return evaluator.EvaluateAsync(request, cancellationToken);
        }

        // AI marking modes are not yet implemented — return a safe completion-only result
        // so the submission is not blocked. Phase 4 will replace this.
        _logger.LogInformation(
            "PatternEvaluationRouter: MarkingMode={MarkingMode} not yet implemented for ActivityId={ActivityId} — returning pending-ai result",
            request.MarkingMode, request.ActivityId);

        var pending = PatternEvaluationResult.Create(
            score: 0,
            maxScore: 0,
            passed: false,
            completed: true,
            coachSummary: "Your answer has been saved. AI evaluation will be available soon.");

        return Task.FromResult(pending);
    }
}
