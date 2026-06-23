namespace LinguaCoach.Application.Admin;

public sealed record AdminAuthEventListQuery(
    int Page = 1,
    int PageSize = 50,
    Guid? UserId = null,
    string? EmailSearch = null,
    string? EventType = null,
    string? Outcome = null,
    DateTime? From = null,
    DateTime? To = null);

public sealed record AdminAuthEventItem(
    Guid Id,
    string EventType,
    string Outcome,
    Guid? UserId,
    string? EmailOrUserName,
    string? FailureReasonCode,
    string? IpAddress,
    string? CorrelationId,
    DateTime OccurredAtUtc);

public interface IAdminAuthEventHandler
{
    Task<PagedResponse<AdminAuthEventItem>> ListAsync(
        AdminAuthEventListQuery query, CancellationToken ct = default);
}
