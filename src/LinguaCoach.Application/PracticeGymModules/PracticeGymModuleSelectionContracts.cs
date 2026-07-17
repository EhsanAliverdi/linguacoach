namespace LinguaCoach.Application.PracticeGymModules;

/// <summary>
/// Phase H7 — input to the deterministic Practice Gym module selector. Pure read-only signal set;
/// the selector never mutates a <c>Module</c>/<c>Lesson</c>/<c>Exercise</c>
/// and never creates Module attempts, mastery updates, or Practice Gym runtime records. Mirrors
/// H6's <c>TodayPlanModuleSelectionRequest</c>, extended with Practice Gym-specific
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
    IReadOnlyList<Guid>? RecentSuggestedModuleIds = null,
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
    Guid ModuleId,
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
    IReadOnlyList<PracticeGymModuleLessonSummary> LinkedLessonSummaries,
    IReadOnlyList<PracticeGymModuleActivitySummary> LinkedActivitySummaries,
    /// <summary>Phase H10 — true when at least one linked Exercise can actually be
    /// launched into a real, runnable practice attempt right now (see
    /// <c>ExerciseLaunch.ExerciseLaunchEligibility</c>). False for every
    /// suggestion before H10; the student Practice Gym UI shows "Start" only when this is true.</summary>
    bool CanLaunch = false,
    /// <summary>Student-safe explanation for why <see cref="CanLaunch"/> is false — e.g. "This
    /// module contains an activity type that is not launchable yet." Null when
    /// <see cref="CanLaunch"/> is true.</summary>
    string? UnsupportedReason = null);

/// <summary>Student-safe projection of a <c>Lesson</c> — no admin-only fields.</summary>
public sealed record PracticeGymModuleLessonSummary(
    Guid LessonId,
    string Title,
    string Body,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> CommonMistakes,
    string? UsageNotes);

/// <summary>Student-safe projection of an <c>Exercise</c>. Deliberately excludes
/// <c>AnswerKeyJson</c> and <c>ScoringRulesJson</c> — those are backend-only per
/// <c>Exercise</c>'s own doc comments and must never reach this view.</summary>
public sealed record PracticeGymModuleActivitySummary(
    Guid ExerciseId,
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

/// <summary>Phase rehaul (2026-07-17) — fleet-wide, read-only aggregate over
/// <c>StudentPracticeGymModuleAssignment</c> for the admin "Today Delivery Health" page. Mirrors
/// <c>TodayPlanDeliveryHealthResult</c>; "suggested" here means at least one Module was suggested
/// (<see cref="Domain.Enums.PracticeGymModuleAssignmentStatus.Suggested"/> or later in the
/// lifecycle), analogous to Today's "Selected".</summary>
public sealed record PracticeGymDeliveryHealthResult(
    PracticeGymDeliveryHealthToday Today,
    IReadOnlyList<PracticeGymDeliveryHealthCefrBucket> ByCefrLevel,
    IReadOnlyList<PracticeGymDeliveryHealthTrendBucket> Trend,
    IReadOnlyList<PracticeGymDeliveryHealthFallbackReason> TopFallbackReasons,
    IReadOnlyList<PracticeGymDeliveryHealthBankCoverage> BankCoverage);

public sealed record PracticeGymDeliveryHealthToday(
    int EligibleStudents,
    int SelectedCount,
    int FallbackOnlyCount,
    int NoAssignmentCount);

public sealed record PracticeGymDeliveryHealthCefrBucket(
    string CefrLevel,
    int EligibleStudents,
    int SelectedCount,
    int FallbackOnlyCount);

public sealed record PracticeGymDeliveryHealthTrendBucket(
    DateTime Date,
    int SelectedCount,
    int FallbackOnlyCount);

public sealed record PracticeGymDeliveryHealthFallbackReason(
    string Reason,
    int Count);

public sealed record PracticeGymDeliveryHealthBankCoverage(
    string CefrLevel,
    int EligibleStudents,
    int ApprovedModuleCount);
