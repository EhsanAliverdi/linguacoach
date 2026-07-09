namespace LinguaCoach.Application.DailyLessonModules;

/// <summary>
/// Phase H6 — input to the deterministic Daily Lesson module selector. Pure read-only signal set;
/// the selector never mutates a <c>ModuleDefinition</c>/<c>LearnItem</c>/<c>ActivityDefinition</c>
/// and never creates Practice Gym or attempt records.
/// </summary>
public sealed record DailyLessonModuleSelectionRequest(
    Guid StudentId,
    string? CefrLevel,
    Guid? LearningPlanId,
    DateTime TargetDate,
    int? PreferredSessionLengthMinutes = null,
    string? RequestedSkill = null,
    IReadOnlyList<string>? LearningGoals = null,
    IReadOnlyList<string>? FocusAreas = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<Guid>? RecentAssignedModuleDefinitionIds = null,
    bool AllowFallback = true,
    int MaxModules = 1);

/// <summary>Phase H6 — result of Daily Lesson module selection. When <see cref="FallbackRequired"/>
/// is true, <see cref="SelectedModules"/> is empty and the caller must fall back to existing Today
/// behavior — this type never represents a hard failure.</summary>
public sealed record DailyLessonModuleSelectionResult(
    IReadOnlyList<SelectedModuleResult> SelectedModules,
    bool FallbackRequired,
    string? FallbackReason,
    string? SelectionReason,
    string? TargetCefrLevel,
    int TotalEstimatedMinutes,
    IReadOnlyList<string> Warnings);

public sealed record SelectedModuleResult(
    Guid ModuleDefinitionId,
    string Title,
    string? Description,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    int? DifficultyBand,
    int? EstimatedMinutes,
    string Reason,
    IReadOnlyList<DailyLessonLearnItemView> LinkedLearnItems,
    IReadOnlyList<DailyLessonActivityView> LinkedActivityDefinitions);

/// <summary>Student-safe projection of a <c>LearnItem</c> — no admin-only fields.</summary>
public sealed record DailyLessonLearnItemView(
    Guid LearnItemId,
    string Title,
    string Body,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> CommonMistakes,
    string? UsageNotes);

/// <summary>Student-safe projection of an <c>ActivityDefinition</c>. Deliberately excludes
/// <c>AnswerKeyJson</c> and <c>ScoringRulesJson</c> — those are backend-only per
/// <c>ActivityDefinition</c>'s own doc comments and must never reach this view.</summary>
public sealed record DailyLessonActivityView(
    Guid ActivityDefinitionId,
    string Title,
    string? Description,
    string Instructions,
    string ActivityType,
    string? FormSchemaJson,
    int? EstimatedMinutes);

public interface IDailyLessonModuleSelectionService
{
    /// <summary>Pure/read-only: performs no database writes. Never throws for "no suitable content"
    /// — returns <see cref="DailyLessonModuleSelectionResult.FallbackRequired"/> instead.</summary>
    Task<DailyLessonModuleSelectionResult> SelectAsync(
        DailyLessonModuleSelectionRequest request, CancellationToken ct = default);
}

/// <summary>Phase H6 — the one write path for the Daily Lesson module pipeline: idempotent per-day
/// upsert of a <c>StudentDailyModuleAssignment</c> bookkeeping row. Kept separate from
/// <see cref="IDailyLessonModuleSelectionService"/> so the selector itself stays pure/read-only.</summary>
public interface IDailyLessonModuleAssignmentRecorder
{
    Task RecordAsync(
        Guid studentId,
        DateTime targetDate,
        DailyLessonModuleSelectionResult selectionResult,
        CancellationToken ct = default);
}
