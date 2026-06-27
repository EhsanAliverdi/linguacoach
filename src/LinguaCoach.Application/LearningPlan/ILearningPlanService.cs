using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.LearningPlan;

/// <summary>
/// Orchestrates generation and refresh of a student's personalized learning plan.
/// Deterministic — no direct AI calls.
/// Coordinates: CurriculumRouting, StudentMastery, ReadinessPool, LearnerPreferences.
/// </summary>
public interface ILearningPlanService
{
    /// <summary>
    /// Returns the student's current active plan summary, generating one if none exists.
    /// </summary>
    Task<LearningPlanSummary> GetOrCreatePlanAsync(
        Guid studentProfileId,
        CancellationToken ct = default);

    /// <summary>
    /// Regenerates the student's learning plan.
    /// Called after mastery change, CEFR change, preference change, or curriculum change.
    /// </summary>
    Task<LearningPlanSummary> RegeneratePlanAsync(
        Guid studentProfileId,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the current progress summary derived from the active plan.
    /// Uses existing mastery data — does not fabricate progress.
    /// </summary>
    Task<LearningPlanProgressSummary> GetProgressAsync(
        Guid studentProfileId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the next curriculum objective key the lesson queue should target.
    /// Replaces independent objective selection in LessonBatchGenerationJob.
    /// Returns null when no active plan exists or no objectives are queued.
    /// </summary>
    Task<PlannedObjectiveContext?> GetNextPlannedObjectiveAsync(
        Guid studentProfileId,
        string? preferredSkill = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns ordered Practice Gym objectives aligned with the learning plan.
    /// Priority: current objectives → weak skills → review → reinforcement.
    /// </summary>
    Task<IReadOnlyList<PlannedObjectiveContext>> GetPracticeGymObjectivesAsync(
        Guid studentProfileId,
        int maxCount = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a planned objective as InProgress when routing has successfully consumed it.
    /// Only transitions from Active → InProgress; no-op for other statuses.
    /// Called by generation jobs after CurriculumRoutingService returns RoutingReason.LearningPlan.
    /// </summary>
    Task MarkObjectiveInProgressAsync(
        Guid studentProfileId,
        string objectiveKey,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a planned objective Completed based on sufficient learning evidence (e.g. NeedsReview signal).
    /// Idempotent — no-op when already Completed or Mastered.
    /// After completing, activates the next planned objective if none are Active.
    /// </summary>
    Task MarkObjectiveCompletedAsync(
        Guid studentProfileId,
        string objectiveKey,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a planned objective Mastered based on the mastery engine signal.
    /// Idempotent — no-op when already Mastered.
    /// After mastering, activates the next planned objective if none are Active.
    /// </summary>
    Task MarkObjectiveMasteredAsync(
        Guid studentProfileId,
        string objectiveKey,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates mastery for a single objective immediately after an activity attempt
    /// and updates the Learning Plan if sufficient evidence exists.
    /// Always returns a result — never throws. Best-effort; must not fail the enclosing submission.
    /// Called from ActivitySubmitHandler after the learning event is recorded.
    /// </summary>
    Task<LearningPlanObjectiveProgressUpdate> TryUpdateObjectiveProgressAsync(
        Guid studentProfileId,
        string objectiveKey,
        CancellationToken ct = default);
}

/// <summary>Summary of the student's active learning plan for API and admin views.</summary>
public sealed record LearningPlanSummary(
    Guid PlanId,
    Guid StudentProfileId,
    string CefrLevel,
    LearningPlanStatus Status,
    string RegenerationReason,
    int RegenerationCount,
    int TotalObjectives,
    int ActiveObjectives,
    int ReviewObjectives,
    int BlockedObjectives,
    int MasteredObjectives,
    int CompletedObjectives,
    int PlannedLessonCount,
    DateTime? LastEvaluatedAt,
    IReadOnlyList<PlannedObjectiveContext> UpcomingObjectives);

/// <summary>Progress metrics for the student's learning journey.</summary>
public sealed record LearningPlanProgressSummary(
    Guid StudentProfileId,
    string CurrentCefrLevel,
    int TotalObjectives,
    int ObjectivesCompleted,
    int ObjectivesMastered,
    int ObjectivesInProgress,
    int ObjectivesRemaining,
    int ReviewObjectives,
    int BlockedObjectives,
    int DeferredObjectives,
    double CompletionPercentage,
    double MasteryPercentage,
    string CurrentLearningPhase,
    int LessonQueueLength,
    int LessonQueueTarget,
    DateTime? LastCompletedAt,
    /// <summary>Key of the current InProgress objective, or first Active objective if none InProgress.</summary>
    string? CurrentObjectiveKey,
    /// <summary>Key of the next Active objective in planned order after the current one.</summary>
    string? NextObjectiveKey,
    /// <summary>Count of objectives completed or mastered since midnight UTC today.</summary>
    int ObjectivesCompletedToday);

/// <summary>Result of a real-time Learning Plan objective progress update.</summary>
public sealed record LearningPlanObjectiveProgressUpdate(
    string ObjectiveKey,
    LearningPlanObjectiveStatus? PreviousStatus,
    LearningPlanObjectiveStatus? NewStatus,
    bool StatusChanged,
    string Reason);

/// <summary>A resolved curriculum objective with routing context for generation.</summary>
public sealed record PlannedObjectiveContext(
    string ObjectiveKey,
    string CefrLevel,
    string PrimarySkill,
    IReadOnlyList<string> SecondarySkills,
    IReadOnlyList<string> ContextTags,
    bool IsReview,
    int Priority,
    string Source);
