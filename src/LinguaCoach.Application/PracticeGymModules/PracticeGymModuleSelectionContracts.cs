namespace LinguaCoach.Application.PracticeGymModules;

/// <summary>
/// Phase H7 — input to the deterministic Practice Gym module selector. Pure read-only signal set;
/// the selector never mutates a <c>ModuleDefinition</c>/<c>LearnItem</c>/<c>ActivityDefinition</c>
/// and never creates Module attempts, mastery updates, or Practice Gym runtime records. Mirrors
/// H6's <c>DailyLessonModuleSelectionRequest</c>, extended with Practice Gym-specific
/// self-directed signals (a student can request a skill/subskill/objective directly, unlike
/// Today's fully automatic selection).
/// </summary>
public sealed record PracticeGymModuleSelectionRequest(
    Guid StudentId,
    string? CefrLevel,
    string? RequestedSkill = null,
    string? RequestedSubskill = null,
    string? RequestedObjectiveKey = null,
    int? RequestedDifficulty = null,
    IReadOnlyList<string>? LearningGoals = null,
    IReadOnlyList<string>? FocusAreas = null,
    IReadOnlyList<string>? ContextTags = null,
    /// <summary>Weak-skill labels (e.g. from <c>IStudentLearningLedger.GetWeakEventsAsync</c>),
    /// where safely available. Soft preference only — never required.</summary>
    IReadOnlyList<string>? WeaknessSignals = null,
    /// <summary>Module ids recently suggested to this student (in addition to the selector's own
    /// 14-day lookback via <c>StudentPracticeGymModuleAssignment</c>).</summary>
    IReadOnlyList<Guid>? RecentSuggestedModuleDefinitionIds = null,
    bool AllowFallback = true,
    int MaxSuggestions = 4);

/// <summary>Phase H7 — result of Practice Gym module selection. When <see cref="FallbackRequired"/>
/// is true, <see cref="Suggestions"/> is empty and the caller must fall back to the existing
/// Practice Gym suggestion pipeline — this type never represents a hard failure.</summary>
public sealed record PracticeGymModuleSelectionResult(
    IReadOnlyList<PracticeGymModuleSuggestion> Suggestions,
    bool FallbackRequired,
    string? FallbackReason,
    string? SelectionReason,
    string? TargetCefrLevel,
    IReadOnlyList<string> Warnings);

public sealed record PracticeGymModuleSuggestion(
    Guid ModuleDefinitionId,
    string Title,
    string? Description,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    int? DifficultyBand,
    int? EstimatedMinutes,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    string Reason,
    bool IsReview,
    bool IsScaffold,
    bool IsRemediation,
    IReadOnlyList<PracticeGymModuleLearnItemSummary> LinkedLearnItemSummaries,
    IReadOnlyList<PracticeGymModuleActivitySummary> LinkedActivitySummaries);

/// <summary>Student-safe projection of a <c>LearnItem</c> — no admin-only fields.</summary>
public sealed record PracticeGymModuleLearnItemSummary(
    Guid LearnItemId,
    string Title,
    string Body,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> CommonMistakes,
    string? UsageNotes);

/// <summary>Student-safe projection of an <c>ActivityDefinition</c>. Deliberately excludes
/// <c>AnswerKeyJson</c> and <c>ScoringRulesJson</c> — those are backend-only per
/// <c>ActivityDefinition</c>'s own doc comments and must never reach this view.</summary>
public sealed record PracticeGymModuleActivitySummary(
    Guid ActivityDefinitionId,
    string Title,
    string? Description,
    string Instructions,
    string ActivityType,
    string? FormSchemaJson,
    int? EstimatedMinutes);

public interface IPracticeGymModuleSelectionService
{
    /// <summary>Pure/read-only: performs no database writes. Never throws for "no suitable
    /// content" — returns <see cref="PracticeGymModuleSelectionResult.FallbackRequired"/>
    /// instead.</summary>
    Task<PracticeGymModuleSelectionResult> SelectAsync(
        PracticeGymModuleSelectionRequest request, CancellationToken ct = default);
}

/// <summary>Phase H7 — the one write path for the Practice Gym module pipeline: records a
/// <c>StudentPracticeGymModuleAssignment</c> row per suggested Module. Kept separate from
/// <see cref="IPracticeGymModuleSelectionService"/> so the selector itself stays pure/read-only.
/// Idempotent per student per calendar day (same convention as H6's assignment recorder) — since
/// Practice Gym suggestions are recomputed on every page load, without this the table would grow
/// unbounded and a module suggested seconds ago would immediately look "recently suggested" to
/// the 14-day reuse guard.</summary>
public interface IPracticeGymModuleAssignmentRecorder
{
    Task RecordAsync(
        Guid studentId,
        PracticeGymModuleSelectionResult selectionResult,
        CancellationToken ct = default);
}
