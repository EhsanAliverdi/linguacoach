namespace LinguaCoach.Application.Dashboard;

public sealed record StudentDashboardSummaryQuery(Guid UserId);

// ── Section records ────────────────────────────────────────────────────────────

public sealed record DashboardSummaryProfile(
    string DisplayName,
    string? CefrLevel,
    string? SupportLanguage);

public sealed record DashboardSummaryCourseReadiness(
    bool IsLearningReady,
    string LifecycleStatus,
    bool PlacementRequired,
    bool LearningPlanExists);

public sealed record DashboardSummaryTodaySession(
    string Status,              // Ready | InProgress | Completed | Preparing | NotAvailable
    Guid? SessionId,
    string? Title,
    string? Topic,
    string? SessionGoal,
    string? FocusSkill,
    int? DurationMinutes,
    int? ExerciseCount,
    string ActionLabel);

public sealed record DashboardSummaryLearningPlan(
    string? PathTitle,
    string? CurrentObjective,
    string? CurrentObjectiveDescription,
    int ObjectiveIndex,
    int TotalObjectives,
    int ModulesCompleted,
    int RemainingObjectives,
    int CompletedActivities,
    int TotalActivities,
    int ProgressPercent);

public sealed record DashboardSummaryPracticeItem(
    Guid ReadinessItemId,
    string Title,
    string Description,
    string? PrimarySkill,
    string CallToAction);

public sealed record DashboardSummaryPractice(
    string Status,              // Ready | Preparing | NotAvailable
    DashboardSummaryPracticeItem? SuggestedItem,
    int ReviewQueueCount,
    string? WeakestSkill);

public sealed record DashboardSummarySkillItem(
    string SkillKey,
    string SkillLabel,
    bool IsWeak,
    int ScorePercent);

public sealed record DashboardSummaryProgress(
    IReadOnlyList<DashboardSummarySkillItem> SkillProfile,
    IReadOnlyList<string> StrongSkills,
    IReadOnlyList<string> WeakSkills,
    IReadOnlyList<string> NextRecommendedFocus,
    string? JourneySummary,
    int ActivitiesCompleted,
    int StreakDays);

public sealed record DashboardSummaryQuickStats(
    string? CurrentCefr,
    int StreakDays,
    int ActivitiesCompleted,
    int ReviewQueueCount);

public sealed record DashboardSummaryWarnings(
    bool MissingLearningPlan,
    bool MissingTodaySession,
    bool PracticeUnavailable,
    bool PlacementIncomplete);

public sealed record StudentDashboardSummaryResult(
    DashboardSummaryProfile Profile,
    DashboardSummaryCourseReadiness CourseReadiness,
    DashboardSummaryTodaySession TodaySession,
    DashboardSummaryLearningPlan LearningPlan,
    DashboardSummaryPractice Practice,
    DashboardSummaryProgress Progress,
    DashboardSummaryQuickStats QuickStats,
    DashboardSummaryWarnings Warnings);

// ── Interface ──────────────────────────────────────────────────────────────────

public interface IStudentDashboardSummaryHandler
{
    Task<StudentDashboardSummaryResult> HandleAsync(
        StudentDashboardSummaryQuery query, CancellationToken ct = default);
}
