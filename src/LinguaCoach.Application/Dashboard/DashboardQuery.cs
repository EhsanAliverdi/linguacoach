namespace LinguaCoach.Application.Dashboard;

public sealed record DashboardQuery(Guid UserId);

public sealed record DashboardResult(
    string StudentName,
    string CareerProfileName,
    string? CefrLevel,
    string Message,
    DashboardLearningPathSummary? LearningPath = null);

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
    int TotalActivities);

public interface IDashboardQueryHandler
{
    Task<DashboardResult> HandleAsync(DashboardQuery query, CancellationToken ct = default);
}
