using LinguaCoach.Application.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminDashboardAggregateHandler : IAdminDashboardAggregateHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminDashboardAggregateHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    private static (int days, string label) ParsePeriod(string period) => period switch
    {
        "7d"  => (7,  "7d"),
        "90d" => (90, "90d"),
        _     => (30, "30d"),
    };

    public async Task<AdminDashboardActivityTrendResponse> GetActivityTrendsAsync(
        string period, CancellationToken ct = default)
    {
        var (days, label) = ParsePeriod(period);
        var start = DateTime.UtcNow.Date.AddDays(-days);
        var end   = DateTime.UtcNow.Date;

        var rows = await _db.ActivityAttempts
            .Where(a => a.DeletedAtUtc == null && a.CreatedAt >= start)
            .Select(a => new
            {
                Date      = a.CreatedAt.Date,
                Completed = a.Completed == true,
                Failed    = a.Completed == true && (a.Passed == false || a.Score < 50),
            })
            .ToListAsync(ct);

        var byDate = rows
            .GroupBy(r => r.Date)
            .ToDictionary(g => g.Key, g => (
                Total:     g.Count(),
                Completed: g.Count(x => x.Completed),
                Failed:    g.Count(x => x.Failed)));

        var buckets = Enumerable.Range(0, days + 1)
            .Select(i =>
            {
                var d = start.AddDays(i);
                byDate.TryGetValue(d, out var v);
                return new ActivityTrendBucket(
                    d.ToString("yyyy-MM-dd"),
                    v.Total,
                    v.Completed,
                    v.Failed);
            })
            .ToList();

        return new AdminDashboardActivityTrendResponse(label, buckets);
    }

    public async Task<AdminDashboardScoreDistributionResponse> GetScoreDistributionAsync(
        string period, CancellationToken ct = default)
    {
        var (days, label) = ParsePeriod(period);
        var start = DateTime.UtcNow.Date.AddDays(-days);

        var scores = await _db.ActivityAttempts
            .Where(a => a.DeletedAtUtc == null && a.Score != null && a.CreatedAt >= start)
            .Select(a => (double)a.Score!)
            .ToListAsync(ct);

        static int Count(IList<double> s, double min, double max) =>
            s.Count(x => x >= min && x <= max);

        var buckets = new List<ScoreDistributionBucket>
        {
            new("0–39",   0,  39,  Count(scores, 0, 39.99)),
            new("40–59",  40, 59,  Count(scores, 40, 59.99)),
            new("60–74",  60, 74,  Count(scores, 60, 74.99)),
            new("75–89",  75, 89,  Count(scores, 75, 89.99)),
            new("90–100", 90, 100, Count(scores, 90, 100)),
        };

        return new AdminDashboardScoreDistributionResponse(label, scores.Count, buckets);
    }

    public async Task<AdminAiUsageTrendResponse> GetAiUsageTrendsAsync(
        string period, CancellationToken ct = default)
    {
        var (days, label) = ParsePeriod(period);
        var start = DateTime.UtcNow.Date.AddDays(-days);

        var rows = await _db.AiUsageLogs
            .Where(l => l.CreatedAt >= start)
            .Select(l => new
            {
                Date       = l.CreatedAt.Date,
                Successful = l.WasSuccessful,
                Input      = (long)l.InputTokens,
                Output     = (long)l.OutputTokens,
                Cost       = l.CostUsd,
            })
            .ToListAsync(ct);

        var byDate = rows
            .GroupBy(r => r.Date)
            .ToDictionary(g => g.Key, g => new
            {
                RequestCount    = g.Count(),
                SuccessfulCalls = g.Count(x => x.Successful),
                FailedCalls     = g.Count(x => !x.Successful),
                InputTokens     = g.Sum(x => x.Input),
                OutputTokens    = g.Sum(x => x.Output),
                Cost            = g.Sum(x => x.Cost),
            });

        var buckets = Enumerable.Range(0, days + 1)
            .Select(i =>
            {
                var d = start.AddDays(i);
                byDate.TryGetValue(d, out var v);
                var inp  = v?.InputTokens  ?? 0;
                var outp = v?.OutputTokens ?? 0;
                return new AdminAggAiUsageTrendBucket(
                    d.ToString("yyyy-MM-dd"),
                    v?.RequestCount     ?? 0,
                    v?.SuccessfulCalls  ?? 0,
                    v?.FailedCalls      ?? 0,
                    inp,
                    outp,
                    inp + outp,
                    v?.Cost ?? 0m);
            })
            .ToList();

        return new AdminAiUsageTrendResponse(label, buckets);
    }

    public async Task<AdminAiUsageCategoryBreakdownResponse> GetAiUsageCategoryBreakdownAsync(
        string period, CancellationToken ct = default)
    {
        var (days, label) = ParsePeriod(period);
        var start = DateTime.UtcNow.Date.AddDays(-days);

        var rawGroups = await _db.AiUsageLogs
            .Where(l => l.CreatedAt >= start)
            .Select(l => new
            {
                l.FeatureKey,
                l.WasSuccessful,
                l.InputTokens,
                l.OutputTokens,
                l.CostUsd,
            })
            .ToListAsync(ct);

        var categories = rawGroups
            .GroupBy(l => l.FeatureKey)
            .Select(g => new AiUsageCategoryBreakdownItem(
                g.Key,
                g.Count(),
                g.Sum(x => (long)x.InputTokens + (long)x.OutputTokens),
                g.Sum(x => x.CostUsd),
                g.Count(x => !x.WasSuccessful)))
            .OrderByDescending(x => x.RequestCount)
            .ToList();

        return new AdminAiUsageCategoryBreakdownResponse(label, categories);
    }
}
