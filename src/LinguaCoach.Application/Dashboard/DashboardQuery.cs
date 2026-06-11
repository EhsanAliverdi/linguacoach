namespace LinguaCoach.Application.Dashboard;

public sealed record DashboardQuery(Guid UserId);

public sealed record DashboardResult(
    string StudentName,
    string CareerProfileName,
    string? CefrLevel,
    string Message,
    string LifecycleStage = "CourseReady",
    DashboardLearningPathSummary? LearningPath = null,
    DashboardActivityStats? ActivityStats = null,
    DashboardFocusArea? CurrentFocus = null,
    string? NextRecommendedPractice = null,
    string? LatestImprovement = null,
    int StreakDays = 0);

public sealed record DashboardActivityStats(
    int ActivitiesCompleted,
    double? LatestScore,
    double? AverageScore);

public sealed record DashboardFocusArea(
    string Category,
    string FriendlyLabel);

public sealed record DashboardLearningPathSummary(
    Guid PathId,
    string Title,
    DashboardModuleSummary? CurrentModule,
    int ModulesCompleted,
    int TotalModules);

public sealed record DashboardModuleSummary(
    Guid ModuleId,
    string Title,
    string Description,
    int Order,
    int CompletedActivities,
    int TotalActivities,
    bool IsReadyToComplete = false,
    double? AverageScore = null);

public interface IDashboardQueryHandler
{
    Task<DashboardResult> HandleAsync(DashboardQuery query, CancellationToken ct = default);
}
