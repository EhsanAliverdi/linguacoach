using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminAuthEventHandler : IAdminAuthEventHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminAuthEventHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<PagedResponse<AdminAuthEventItem>> ListAsync(
        AdminAuthEventListQuery query, CancellationToken ct = default)
    {
        var q = _db.AuthSecurityEvents.AsNoTracking();

        if (query.UserId.HasValue)
            q = q.Where(e => e.UserId == query.UserId.Value);

        if (!string.IsNullOrWhiteSpace(query.EmailSearch))
            q = q.Where(e => e.EmailOrUserName != null &&
                              e.EmailOrUserName.Contains(query.EmailSearch.ToLowerInvariant()));

        if (!string.IsNullOrWhiteSpace(query.EventType) &&
            Enum.TryParse<AuthEventType>(query.EventType, ignoreCase: true, out var et))
            q = q.Where(e => e.EventType == et);

        if (!string.IsNullOrWhiteSpace(query.Outcome) &&
            Enum.TryParse<AuthEventOutcome>(query.Outcome, ignoreCase: true, out var oc))
            q = q.Where(e => e.Outcome == oc);

        if (query.From.HasValue)
            q = q.Where(e => e.OccurredAtUtc >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(e => e.OccurredAtUtc <= query.To.Value);

        var total = await q.CountAsync(ct);

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderByDescending(e => e.OccurredAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(e => new AdminAuthEventItem(
                e.Id,
                e.EventType.ToString(),
                e.Outcome.ToString(),
                e.UserId,
                e.EmailOrUserName,
                e.FailureReasonCode,
                e.IpAddress,
                e.CorrelationId,
                e.OccurredAtUtc))
            .ToListAsync(ct);

        var totalPages = size > 0 ? (int)Math.Ceiling(total / (double)size) : 1;
        return new PagedResponse<AdminAuthEventItem>(items, total, page, size, totalPages);
    }
}
