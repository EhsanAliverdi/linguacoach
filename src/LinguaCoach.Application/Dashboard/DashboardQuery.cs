namespace LinguaCoach.Application.Dashboard;

public sealed record DashboardQuery(Guid UserId);

public sealed record DashboardResult(
    string StudentName,
    string CareerProfileName,
    string? CefrLevel,
    string Message);

public interface IDashboardQueryHandler
{
    Task<DashboardResult> HandleAsync(DashboardQuery query, CancellationToken ct = default);
}
