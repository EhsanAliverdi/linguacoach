namespace LinguaCoach.Application.TodayPlanModules;

/// <summary>
/// Phase H6 (renamed I4 Pass 3) — input to the deterministic Today Plan module selector. Pure
/// read-only signal set; the selector never mutates a <c>Module</c>/<c>Lesson</c>/<c>Exercise</c>
/// and never creates Practice Gym or attempt records.
/// </summary>
public sealed record TodayPlanModuleSelectionRequest(
    Guid StudentId,
    string? CefrLevel,
    Guid? LearningPlanId,
    DateTime TargetDate,
    int? PreferredSessionLengthMinutes = null,
    string? RequestedSkill = null,
    IReadOnlyList<string>? LearningGoals = null,
    IReadOnlyList<string>? FocusAreas = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<Guid>? RecentAssignedModuleIds = null,
    bool AllowFallback = true,
    int MaxModules = 1);

/// <summary>Phase H6 (renamed I4 Pass 3) — result of Today Plan module selection. When
/// <see cref="FallbackRequired"/> is true, <see cref="SelectedModules"/> is empty and the caller
/// must fall back to existing Today behavior — this type never represents a hard failure.</summary>
public sealed record TodayPlanModuleSelectionResult(
    IReadOnlyList<SelectedModuleResult> SelectedModules,
    bool FallbackRequired,
    string? FallbackReason,
    string? SelectionReason,
    string? TargetCefrLevel,
    int TotalEstimatedMinutes,
    IReadOnlyList<string> Warnings);

public sealed record SelectedModuleResult(
    Guid ModuleId,
    string Title,
    string? Description,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    int? DifficultyBand,
    int? EstimatedMinutes,
    string Reason,
    IReadOnlyList<TodayPlanLessonView> LinkedLessons,
    IReadOnlyList<TodayPlanActivityView> LinkedExercises);

/// <summary>Student-safe projection of a <c>Lesson</c> — no admin-only fields.</summary>
public sealed record TodayPlanLessonView(
    Guid LessonId,
    string Title,
    string Body,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> CommonMistakes,
    string? UsageNotes);

/// <summary>Student-safe projection of an <c>Exercise</c>. Deliberately excludes
/// <c>AnswerKeyJson</c> and <c>ScoringRulesJson</c> — those are backend-only per
/// <c>Exercise</c>'s own doc comments and must never reach this view.</summary>
public sealed record TodayPlanActivityView(
    Guid ExerciseId,
    string Title,
    string? Description,
    string Instructions,
    string ActivityType,
    string? FormSchemaJson,
    int? EstimatedMinutes);

public interface ITodayPlanModuleSelectionService
{
    /// <summary>Pure/read-only: performs no database writes. Never throws for "no suitable content"
    /// — returns <see cref="TodayPlanModuleSelectionResult.FallbackRequired"/> instead.</summary>
    Task<TodayPlanModuleSelectionResult> SelectAsync(
        TodayPlanModuleSelectionRequest request, CancellationToken ct = default);
}

/// <summary>Phase H6 (renamed I4 Pass 3) — the one write path for the Today Plan module pipeline:
/// idempotent per-day upsert of a <c>StudentTodayPlanModuleAssignment</c> bookkeeping row. Kept
/// separate from <see cref="ITodayPlanModuleSelectionService"/> so the selector itself stays
/// pure/read-only.</summary>
public interface ITodayPlanModuleAssignmentRecorder
{
    Task RecordAsync(
        Guid studentId,
        DateTime targetDate,
        TodayPlanModuleSelectionResult selectionResult,
        CancellationToken ct = default);
}
