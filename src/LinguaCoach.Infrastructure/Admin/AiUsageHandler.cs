using LinguaCoach.Application.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AiUsageHandler : IAdminAiUsageHandler
{
    private readonly LinguaCoachDbContext _db;

    public AiUsageHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AiUsageSummaryDto> GetSummaryAsync(AiUsageDateFilter? filter = null, CancellationToken ct = default)
    {
        var query = _db.AiUsageLogs.AsNoTracking();
        query = ApplyDateFilter(query, filter);
        var logs = await query.ToListAsync(ct);

        var totalCalls = logs.Count;
        var successful = logs.Count(l => l.WasSuccessful);
        var failed = logs.Count(l => !l.WasSuccessful);
        var fallback = logs.Count(l => l.IsFallback);
        var totalCost = logs.Sum(l => l.CostUsd);
        var totalInputTokens = logs.Sum(l => (long)l.InputTokens);
        var totalOutputTokens = logs.Sum(l => (long)l.OutputTokens);

        var byProvider = logs
            .GroupBy(l => l.ProviderName)
            .Select(g => new AiUsageByProvider(
                Provider: g.Key,
                Calls: g.Count(),
                Successful: g.Count(l => l.WasSuccessful),
                Fallback: g.Count(l => l.IsFallback),
                CostUsd: g.Sum(l => l.CostUsd)))
            .OrderByDescending(x => x.Calls)
            .ToList();

        var byFeature = logs
            .GroupBy(l => l.FeatureKey)
            .Select(g => new AiUsageByFeature(
                Feature: g.Key,
                Calls: g.Count(),
                Successful: g.Count(l => l.WasSuccessful),
                CostUsd: g.Sum(l => l.CostUsd)))
            .OrderByDescending(x => x.Calls)
            .ToList();

        return new AiUsageSummaryDto(
            TotalCalls: totalCalls,
            SuccessfulCalls: successful,
            FailedCalls: failed,
            FallbackCalls: fallback,
            TotalCostUsd: totalCost,
            TotalInputTokens: totalInputTokens,
            TotalOutputTokens: totalOutputTokens,
            TotalTokens: totalInputTokens + totalOutputTokens,
            ByProvider: byProvider,
            ByFeature: byFeature);
    }

    public async Task<AiUsagePagedResult> GetRecentAsync(
        int page = 1, int pageSize = 25, AiUsageDateFilter? dateFilter = null, AiUsageRecentFilter? recentFilter = null, CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AiUsageLogs.AsNoTracking();
        query = ApplyDateFilter(query, dateFilter);
        query = ApplyRecentFilter(query, recentFilter);

        var totalCount = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var rows = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(l => new AiUsageRecentItem(
            Id: l.Id,
            CreatedAt: l.CreatedAt,
            StudentProfileId: l.StudentProfileId,
            FeatureKey: l.FeatureKey,
            Provider: l.ProviderName,
            Model: l.ModelName,
            IsFallback: l.IsFallback,
            WasSuccessful: l.WasSuccessful,
            FailureReason: l.FailureReason,
            InputTokens: l.InputTokens,
            OutputTokens: l.OutputTokens,
            CostUsd: l.CostUsd,
            DurationMs: l.DurationMs,
            CorrelationId: l.CorrelationId)).ToList();

        return new AiUsagePagedResult(items, totalCount, page, pageSize, totalPages);
    }

    // From is inclusive (>=), To is exclusive (<). UTC assumed for both.
    private static IQueryable<Domain.Entities.AiUsageLog> ApplyDateFilter(
        IQueryable<Domain.Entities.AiUsageLog> query,
        AiUsageDateFilter? filter)
    {
        if (filter is null) return query;
        if (filter.From.HasValue)
            query = query.Where(l => l.CreatedAt >= filter.From.Value);
        if (filter.To.HasValue)
            query = query.Where(l => l.CreatedAt < filter.To.Value);
        return query;
    }

    // status: "success" = WasSuccessful && !IsFallback, "failed" = !WasSuccessful, "fallback" = IsFallback.
    private static IQueryable<Domain.Entities.AiUsageLog> ApplyRecentFilter(
        IQueryable<Domain.Entities.AiUsageLog> query,
        AiUsageRecentFilter? filter)
    {
        if (filter is null) return query;
        if (!string.IsNullOrEmpty(filter.Provider))
            query = query.Where(l => l.ProviderName == filter.Provider);
        if (!string.IsNullOrEmpty(filter.Model))
            query = query.Where(l => l.ModelName == filter.Model);
        if (!string.IsNullOrEmpty(filter.FeatureKey))
            query = query.Where(l => l.FeatureKey == filter.FeatureKey);
        if (!string.IsNullOrEmpty(filter.Status))
        {
            var s = filter.Status.ToLowerInvariant();
            if (s == "success")  query = query.Where(l => l.WasSuccessful && !l.IsFallback);
            if (s == "failed")   query = query.Where(l => !l.WasSuccessful);
            if (s == "fallback") query = query.Where(l => l.IsFallback);
        }
        if (filter.StudentId.HasValue)
            query = query.Where(l => l.StudentProfileId == filter.StudentId.Value);
        return query;
    }
}
