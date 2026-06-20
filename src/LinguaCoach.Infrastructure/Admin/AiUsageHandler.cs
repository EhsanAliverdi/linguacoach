using LinguaCoach.Application.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AiUsageHandler : IAdminAiUsageHandler
{
    private readonly LinguaCoachDbContext _db;

    public AiUsageHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AiUsageSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var logs = await _db.AiUsageLogs.AsNoTracking().ToListAsync(ct);

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

    public async Task<IReadOnlyList<AiUsageRecentItem>> GetRecentAsync(
        int limit = 100, CancellationToken ct = default)
    {
        var items = await _db.AiUsageLogs.AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(ct);

        return items.Select(l => new AiUsageRecentItem(
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
    }
}
