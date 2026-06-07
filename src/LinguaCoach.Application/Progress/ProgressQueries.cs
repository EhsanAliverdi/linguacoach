namespace LinguaCoach.Application.Progress;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetProgressQuery(Guid UserId);

public interface IGetProgressHandler
{
    Task<ProgressSummaryDto> HandleAsync(GetProgressQuery query, CancellationToken ct = default);
}

// ── Summary stats ─────────────────────────────────────────────────────────────

public sealed record ProgressSummaryDto(
    ProgressStatsDto Stats,
    IReadOnlyList<ScoreTrendPointDto> ScoreTrend,
    ProgressSkillSectionDto SkillProgress,
    ProgressLearningFocusDto? LearningFocus,
    IReadOnlyList<ProgressModuleDto> ModuleProgress);

public sealed record ProgressStatsDto(
    int ActivitiesCompleted,
    int TotalAttempts,
    int RetryAttempts,
    double? AverageScore,
    double? LatestScore,
    double? BestScore,
    int ActivitiesThisWeek,
    int ModulesCompleted,
    ProgressCurrentModuleDto? CurrentModuleProgress);

public sealed record ProgressCurrentModuleDto(
    Guid ModuleId,
    string Title,
    int CompletedActivities,
    int TotalRequired,
    double? AverageScore,
    double? LatestScore,
    bool IsReadyToComplete);

// ── Score trend ───────────────────────────────────────────────────────────────

public sealed record ScoreTrendPointDto(
    DateTime AttemptDate,
    double Score,
    string ActivityTitle,
    string? ModuleTitle,
    int AttemptNumber);

// ── Skill progress ────────────────────────────────────────────────────────────

public sealed record ProgressSkillSectionDto(
    IReadOnlyList<ProgressSkillDto> Skills,
    IReadOnlyList<string> TopStrengths,
    IReadOnlyList<string> WeakestSkills);

public sealed record ProgressSkillDto(
    string SkillKey,
    string SkillLabel,
    bool IsWeak);

// ── Learning focus ────────────────────────────────────────────────────────────

public sealed record ProgressLearningFocusDto(
    string? JourneySummary,
    IReadOnlyList<string> NextRecommendedFocus,
    IReadOnlyList<string> RecurringMistakes,
    IReadOnlyList<string> WeakSkills,
    IReadOnlyList<string> StrongSkills);

// ── Module progress ───────────────────────────────────────────────────────────

public sealed record ProgressModuleDto(
    Guid ModuleId,
    string Title,
    string Status,           // "completed" | "current" | "upcoming"
    int CompletedActivities,
    int TotalRequired,
    double? AverageScore,
    double? LatestScore,
    bool IsReadyToComplete,
    DateTime? CompletedAt);
