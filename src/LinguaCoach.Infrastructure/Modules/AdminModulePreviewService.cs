using System.Text.Json;
using LinguaCoach.Application.ExerciseLaunch;
using LinguaCoach.Application.FormIo;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Modules;

/// <summary>
/// Phase J3 — admin "preview as a learner" for a Module. Loads a Module's linked Lesson + Exercise
/// for rendering (any review status — preview happens before approval), and scores a preview
/// submission using the exact same <see cref="ComponentAnswerScorer"/>/
/// <see cref="ExerciseLaunchEligibility"/> logic the real student runtime uses. Never creates a
/// <see cref="LearningActivity"/>, <see cref="ActivityAttempt"/>, or any student-facing runtime
/// record — entirely separate from <see cref="ExerciseLaunch.IExerciseLaunchService"/>, which is
/// the real launch path reached only after a Module is Approved.
/// </summary>
public sealed class AdminModulePreviewService : IAdminModulePreviewQuery, IAdminModulePreviewSubmitHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminModulePreviewService(LinguaCoachDbContext db) => _db = db;

    public async Task<ModulePreviewResult?> HandleAsync(Guid moduleId, CancellationToken ct = default)
    {
        var module = await _db.Modules.AsNoTracking().FirstOrDefaultAsync(m => m.Id == moduleId, ct);
        if (module is null) return null;

        var lessonLink = await _db.ModuleLessonLinks.AsNoTracking()
            .Where(l => l.ModuleId == moduleId).OrderBy(l => l.SortOrder).FirstOrDefaultAsync(ct);
        var exerciseLinks = await _db.ModuleExerciseLinks.AsNoTracking()
            .Where(l => l.ModuleId == moduleId).OrderBy(l => l.SortOrder).ToListAsync(ct);

        ModulePreviewLessonDto? lessonDto = null;
        if (lessonLink is not null)
        {
            var lesson = await _db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonLink.LessonId, ct);
            if (lesson is not null)
                lessonDto = new ModulePreviewLessonDto(
                    lesson.Id, lesson.Title, lesson.Body,
                    ParseStringArray(lesson.ExamplesJson), ParseStringArray(lesson.CommonMistakesJson), lesson.UsageNotes);
        }

        var exerciseIds = exerciseLinks.Select(l => l.ExerciseId).ToList();
        var exercises = await _db.Exercises.AsNoTracking()
            .Where(a => exerciseIds.Contains(a.Id)).ToListAsync(ct);
        var exerciseById = exercises.ToDictionary(a => a.Id);

        var exerciseDtos = exerciseLinks
            .Where(l => exerciseById.ContainsKey(l.ExerciseId))
            .Select(l =>
            {
                var exercise = exerciseById[l.ExerciseId];
                var eligibility = ExerciseLaunchEligibility.Evaluate(exercise);
                return new ModulePreviewExerciseDto(
                    exercise.Id, exercise.Title, exercise.Instructions, exercise.ActivityType,
                    exercise.RendererType.ToString(), exercise.FormSchemaJson, exercise.EstimatedMinutes,
                    eligibility.CanLaunch, eligibility.UnsupportedReason);
            })
            .ToList();

        return new ModulePreviewResult(
            module.Id, module.Title, module.Description, module.ReviewStatus.ToString(),
            lessonDto, exerciseDtos, module.FeedbackPlanJson);
    }

    public async Task<ModulePreviewSubmitResult> HandleAsync(ModulePreviewSubmitRequest request, CancellationToken ct = default)
    {
        var exerciseLinksQuery = _db.ModuleExerciseLinks.AsNoTracking()
            .Where(l => l.ModuleId == request.ModuleId);
        var exerciseLink = request.ExerciseId is { } requestedExerciseId
            ? await exerciseLinksQuery.FirstOrDefaultAsync(l => l.ExerciseId == requestedExerciseId, ct)
                ?? throw new ModuleValidationException($"Exercise '{requestedExerciseId}' is not linked to Module '{request.ModuleId}'.")
            : await exerciseLinksQuery.OrderBy(l => l.SortOrder).FirstOrDefaultAsync(ct)
                ?? throw new ModuleValidationException($"Module '{request.ModuleId}' has no linked Exercise to preview.");

        var exercise = await _db.Exercises.AsNoTracking().FirstOrDefaultAsync(a => a.Id == exerciseLink.ExerciseId, ct)
            ?? throw new ModuleValidationException($"Exercise '{exerciseLink.ExerciseId}' linked to this Module was not found.");

        var eligibility = ExerciseLaunchEligibility.Evaluate(exercise);
        if (!eligibility.CanLaunch)
            return new ModulePreviewSubmitResult(
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
            return new ModulePreviewSubmitResult(
                Scored: false, UnscorableReason: "This activity has no usable scoring rules.",
                ScorePercent: null, AllCorrect: null, Components: [], FeedbackMessage: null);

        var results = new List<ModulePreviewComponentResult>();
        foreach (var (key, rule) in components)
        {
            var value = request.Answers.TryGetValue(key, out var v) ? v : default;
            var scored = ComponentAnswerScorer.Score(key, rule, value);
            results.Add(new ModulePreviewComponentResult(key, scored.IsCorrect, scored.PointsEarned, scored.MaxPoints));
        }

        var totalEarned = results.Sum(r => r.PointsEarned);
        var totalMax = results.Sum(r => r.MaxPoints);
        var scorePercent = totalMax > 0 ? totalEarned / totalMax * 100.0 : 0.0;
        var allCorrect = results.Count > 0 && results.All(r => r.IsCorrect);

        var feedbackMessage = ExtractFeedbackMessage(exercise.FeedbackPlanJson, allCorrect);

        return new ModulePreviewSubmitResult(
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

    private static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
