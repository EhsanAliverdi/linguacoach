using System.Text.Json;
using LinguaCoach.Application.ExerciseLaunch;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.FormIo;
using LinguaCoach.Application.Placement;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Exercises;

/// <summary>
/// Phase K7 — admin "preview as a learner" for a standalone Exercise, mirroring
/// <see cref="Modules.AdminModulePreviewService"/>'s submit-scoring logic exactly (same
/// <see cref="ComponentAnswerScorer"/>), but reached directly from the Exercise detail page rather
/// than through a Module. Never creates a LearningActivity/ActivityAttempt.
/// </summary>
public sealed class AdminExercisePreviewService : IAdminExercisePreviewSubmitHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminExercisePreviewService(LinguaCoachDbContext db) => _db = db;

    public async Task<ExercisePreviewSubmitResult> HandleAsync(ExercisePreviewSubmitRequest request, CancellationToken ct = default)
    {
        var exercise = await _db.Exercises.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.ExerciseId, ct)
            ?? throw new ExerciseValidationException($"Exercise '{request.ExerciseId}' was not found.");

        var eligibility = ExerciseLaunchEligibility.Evaluate(exercise);
        if (!eligibility.CanLaunch)
            return new ExercisePreviewSubmitResult(
                Scored: false, UnscorableReason: eligibility.UnsupportedReason,
                ScorePercent: null, AllCorrect: null, Components: [], FeedbackMessage: null);

        ScoringRulesDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ScoringRulesDocument>(exercise.ScoringRulesJson!);
        }
        catch (JsonException)
        {
            document = null;
        }

        if (document?.Components is not { Count: > 0 } components)
            return new ExercisePreviewSubmitResult(
                Scored: false, UnscorableReason: "This activity has no usable scoring rules.",
                ScorePercent: null, AllCorrect: null, Components: [], FeedbackMessage: null);

        var results = new List<ExercisePreviewComponentResult>();
        foreach (var (key, rule) in components)
        {
            var value = request.Answers.TryGetValue(key, out var v) ? v : default;
            var scored = ComponentAnswerScorer.Score(key, rule, value);
            results.Add(new ExercisePreviewComponentResult(key, scored.IsCorrect, scored.PointsEarned, scored.MaxPoints));
        }

        var totalEarned = results.Sum(r => r.PointsEarned);
        var totalMax = results.Sum(r => r.MaxPoints);
        var scorePercent = totalMax > 0 ? totalEarned / totalMax * 100.0 : 0.0;
        var allCorrect = results.Count > 0 && results.All(r => r.IsCorrect);

        var feedbackMessage = ExtractFeedbackMessage(exercise.FeedbackPlanJson, allCorrect);

        return new ExercisePreviewSubmitResult(
            Scored: true, UnscorableReason: null, ScorePercent: scorePercent, AllCorrect: allCorrect,
            Components: results, FeedbackMessage: feedbackMessage);
    }

    private static string? ExtractFeedbackMessage(string? feedbackPlanJson, bool allCorrect)
    {
        if (string.IsNullOrWhiteSpace(feedbackPlanJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(feedbackPlanJson);
            var prop = allCorrect ? "correctFeedback" : "incorrectFeedback";
            return doc.RootElement.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
