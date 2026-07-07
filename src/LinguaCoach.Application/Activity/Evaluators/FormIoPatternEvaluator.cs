using System.Text.Json;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity.Evaluators;

/// <summary>
/// Deterministic Form.io component scoring for Practice Gym's Form.io rendering pilot
/// (docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md). No AI call. Reuses
/// IPlacementScoringService — the same scoring engine placement items use — so a Form.io-scored
/// Practice Gym activity is held to exactly the same deterministic single_choice/multiple_choice/
/// text_exact/text_normalized rules, without duplicating that logic.
///
/// request.ContentJson carries the student-safe Form.io schema (LearningActivity.FormIoSchemaJson)
/// — never the correct answers. request.ScoringRulesJson carries the backend-only rules
/// (LearningActivity.ScoringRulesJson), read server-side only and never echoed back to the client.
/// </summary>
public sealed class FormIoPatternEvaluator : IPatternEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPlacementScoringService _scoring;

    public FormIoPatternEvaluator(IPlacementScoringService scoring)
    {
        _scoring = scoring;
    }

    public MarkingMode MarkingMode => MarkingMode.FormIoScored;

    public Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var submissionData = ParseSubmissionData(request.SubmittedAnswerJson);
        var scoreResult = _scoring.ScoreSubmission(request.ScoringRulesJson, submissionData);

        var itemResults = scoreResult.Components
            .Where(c => c.MaxPoints > 0)
            .Select(c => new PatternEvaluationItemResult(
                ItemKey: c.ComponentKey,
                StudentAnswer: c.NormalizedValue,
                CorrectAnswer: null,
                AcceptedAnswers: [],
                IsCorrect: c.IsCorrect,
                Score: c.PointsEarned,
                MaxScore: c.MaxPoints,
                Feedback: c.IsCorrect ? "Correct." : "Incorrect."))
            .ToList();

        var totalScore = itemResults.Sum(i => i.Score);
        var totalMax = itemResults.Sum(i => i.MaxScore);

        var result = PatternEvaluationResult.Create(
            score: totalScore,
            maxScore: totalMax,
            passed: scoreResult.IsCorrect,
            completed: true,
            itemResults: itemResults,
            coachSummary: scoreResult.EvaluationNotes);

        return Task.FromResult(result);
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseSubmissionData(string submittedAnswerJson)
    {
        if (string.IsNullOrWhiteSpace(submittedAnswerJson))
            return new Dictionary<string, JsonElement>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(submittedAnswerJson, JsonOptions)
                ?? new Dictionary<string, JsonElement>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }
}
