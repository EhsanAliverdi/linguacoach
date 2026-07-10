using System.Text.Json;

namespace LinguaCoach.Application.Modules;

// ── Phase J3 — admin "preview as a learner" for a Module. Lets an admin render a Module's
// Lesson + Exercise exactly as a student would, submit an answer, and see a real score/feedback
// — all BEFORE the Module is approved. Works for a Module in any review status (that is the whole
// point: preview happens before approval). Never creates a LearningActivity, ActivityAttempt, or
// any student-facing runtime record — this is a read/score-only admin diagnostic, entirely
// separate from the real student runtime launch path (IExerciseLaunchService). Never exposes
// AnswerKeyJson/ScoringRulesJson to the response — only a student-safe Form.io schema, exactly
// like the real runtime does. ──

public sealed record ModulePreviewLessonDto(
    Guid LessonId,
    string Title,
    string Body,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> CommonMistakes,
    string? UsageNotes);

public sealed record ModulePreviewExerciseDto(
    Guid ExerciseId,
    string Title,
    string Instructions,
    string ActivityType,
    string RendererType,
    string? FormSchemaJson,
    int? EstimatedMinutes,
    /// <summary>True when this Exercise can be auto-scored in preview — same eligibility check
    /// (<see cref="ExerciseLaunch.ExerciseLaunchEligibility"/>) the real student runtime launch
    /// path uses, so preview never claims an activity is scorable when the live path would
    /// disagree.</summary>
    bool CanScore,
    string? UnscorableReason);

public sealed record ModulePreviewResult(
    Guid ModuleId,
    string ModuleTitle,
    string? ModuleDescription,
    string ModuleReviewStatus,
    ModulePreviewLessonDto? Lesson,
    ModulePreviewExerciseDto? Exercise,
    string? ModuleFeedbackPlanJson);

public sealed record ModulePreviewSubmitRequest(
    Guid ModuleId,
    IReadOnlyDictionary<string, JsonElement> Answers);

public sealed record ModulePreviewComponentResult(
    string ComponentKey,
    bool IsCorrect,
    double PointsEarned,
    double MaxPoints);

public sealed record ModulePreviewSubmitResult(
    bool Scored,
    string? UnscorableReason,
    double? ScorePercent,
    bool? AllCorrect,
    IReadOnlyList<ModulePreviewComponentResult> Components,
    string? FeedbackMessage);

/// <summary>Loads a Module's linked Lesson + Exercise for admin preview rendering, regardless of
/// the Module's own review status. Returns null when the Module itself doesn't exist.</summary>
public interface IAdminModulePreviewQuery
{
    Task<ModulePreviewResult?> HandleAsync(Guid moduleId, CancellationToken ct = default);
}

/// <summary>Scores a preview submission against the Exercise's real scoring rules, using the same
/// <see cref="FormIo.ComponentAnswerScorer"/> the real student runtime uses — never invents a
/// separate/simplified scoring path. Returns <see cref="ModulePreviewSubmitResult.Scored"/> = false
/// (never throws for this case) when the linked Exercise isn't auto-scorable, matching
/// <see cref="ExerciseLaunch.ExerciseLaunchEligibility"/>'s reasons exactly.</summary>
public interface IAdminModulePreviewSubmitHandler
{
    Task<ModulePreviewSubmitResult> HandleAsync(ModulePreviewSubmitRequest request, CancellationToken ct = default);
}
