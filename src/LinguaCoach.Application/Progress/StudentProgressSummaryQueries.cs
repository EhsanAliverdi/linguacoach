namespace LinguaCoach.Application.Progress;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetStudentProgressSummaryQuery(Guid UserId);

public interface IStudentProgressSummaryHandler
{
    Task<StudentProgressSummaryDto> HandleAsync(
        GetStudentProgressSummaryQuery query, CancellationToken ct = default);
}

// ── Top-level result ──────────────────────────────────────────────────────────

public sealed record StudentProgressSummaryDto(
    StudentProgressLearningSummaryDto Learning,
    IReadOnlyList<ProgressSkillDto> Skills,
    StudentProgressCefrDto Cefr,
    StudentProgressMasteryDto Mastery,
    IReadOnlyList<ProgressActivityEventDto> RecentActivity,
    StudentProgressFocusDto Focus);

// ── Learning summary (Part C) ─────────────────────────────────────────────────

public sealed record StudentProgressLearningSummaryDto(
    string? CurrentCefrLevel,
    DateTime? PlacementCompletedAt,
    string CurrentLearningPhase,
    int TotalObjectives,
    int ObjectivesCompleted,
    int ObjectivesMastered,
    int ObjectivesInProgress,
    int ObjectivesRemaining,
    double CompletionPercentage,
    string? CurrentObjectiveKey,
    string? CurrentObjectiveSkill,
    int ObjectivesCompletedToday);

// ── CEFR progress (Part E) ────────────────────────────────────────────────────

public sealed record StudentProgressCefrDto(
    string? StartingCefrLevel,
    string? CurrentCefrLevel,
    bool CefrImproved,
    DateTime? PlacementDate,
    string? Note);

// ── Mastery and review (Part F) ───────────────────────────────────────────────

public sealed record StudentProgressMasteryDto(
    int MasteredObjectivesCount,
    int InProgressObjectivesCount,
    int ReviewQueueCount,
    int WeakSkillsCount,
    IReadOnlyList<string> WeakSkillLabels);

// ── Recent activity events (Part G) ──────────────────────────────────────────

public sealed record ProgressActivityEventDto(
    /// <summary>PlacementCompleted | LessonCompleted | PracticeCompleted | ObjectiveMastered | PlanRegenerated</summary>
    string EventType,
    string Description,
    string? Detail,
    DateTime OccurredAt);

// ── Focus recommendations (Part H) ───────────────────────────────────────────

public sealed record StudentProgressFocusDto(
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> RecurringMistakes,
    string? JourneySummary);
