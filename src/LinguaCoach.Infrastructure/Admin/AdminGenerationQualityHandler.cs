using LinguaCoach.Application.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminGenerationQualityHandler : IAdminGenerationQualityHandler
{
    private const int DefaultRetentionDays = 90;
    private const double DefaultWarningThreshold = 0.15;
    private const int DefaultMinimumFailuresForWarning = 5;

    private readonly LinguaCoachDbContext _db;
    private readonly IConfiguration _config;

    public AdminGenerationQualityHandler(LinguaCoachDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<GenerationQualitySummary> GetSummaryAsync(int recentDays = 30, CancellationToken ct = default)
    {
        var retentionDays = _config.GetValue<int?>("GenerationQuality:RetentionDays") ?? DefaultRetentionDays;
        var warningThreshold = _config.GetValue<double?>("GenerationQuality:AbandonedFailureRateWarningThreshold") ?? DefaultWarningThreshold;
        var minimumFailuresForWarning = _config.GetValue<int?>("GenerationQuality:MinimumFailuresForWarning") ?? DefaultMinimumFailuresForWarning;

        var since = DateTime.UtcNow.AddDays(-recentDays);

        var failures = await _db.GenerationValidationFailures
            .AsNoTracking()
            .Where(f => f.CreatedAt >= since)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        var totalFailures = failures.Count;
        var abandonedCount = failures.Count(f => f.AttemptNumber == 2);
        var recentFailureCount = failures.Count(f => f.CreatedAt >= DateTime.UtcNow.AddHours(-24));

        var latestFailures = failures
            .Take(20)
            .Select(f => new ValidationFailureItem(
                f.CreatedAt,
                f.PatternKey,
                f.ActivityTypeName,
                f.CefrLevel,
                f.ObjectiveKey,
                f.ValidationErrors,
                f.AttemptNumber,
                ProviderName: f.ProviderName,
                ModelName: f.ModelName,
                GenerationSource: null,
                CorrelationId: f.CorrelationId))
            .ToList();

        var patternBreakdown = failures
            .Where(f => f.PatternKey is not null)
            .GroupBy(f => f.PatternKey!)
            .Select(g => new PatternFailureBreakdownItem(
                g.Key,
                g.Count(),
                g.Count(f => f.AttemptNumber == 2),
                g.OrderByDescending(f => f.CreatedAt).First().ValidationErrors))
            .OrderByDescending(p => p.TotalFailures)
            .ToList();

        var cefrBreakdown = failures
            .Where(f => f.CefrLevel is not null)
            .GroupBy(f => f.CefrLevel!)
            .Select(g => new CefrFailureBreakdownItem(g.Key, g.Count()))
            .OrderByDescending(c => c.TotalFailures)
            .ToList();

        var providerBreakdown = failures
            .Where(f => f.ProviderName is not null)
            .GroupBy(f => new { Provider = f.ProviderName!, Model = f.ModelName ?? "unknown" })
            .Select(g => new ProviderModelBreakdownItem(
                g.Key.Provider,
                g.Key.Model,
                g.Count(),
                g.Count(f => f.AttemptNumber == 2)))
            .OrderByDescending(p => p.TotalFailures)
            .ToList();

        var abandonedRate = totalFailures >= minimumFailuresForWarning && totalFailures > 0
            ? (double)abandonedCount / totalFailures
            : 0.0;
        var warningActive = totalFailures >= minimumFailuresForWarning && abandonedRate >= warningThreshold;
        var abandonedWarning = new AbandonedGenerationWarning(
            IsActive: warningActive,
            AbandonedRate: abandonedRate,
            AbandonedCount: abandonedCount,
            TotalFailures: totalFailures,
            WarningThreshold: warningThreshold,
            Message: warningActive
                ? $"Abandoned generation rate {abandonedRate:P0} exceeds threshold {warningThreshold:P0}. Review pattern and CEFR breakdowns."
                : null);

        var prompts = await _db.AiPrompts
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Key)
            .ToListAsync(ct);

        var promptSummary = prompts
            .Select(p => new PromptTemplateItem(
                p.Id, p.Key, p.Version, p.IsActive, p.MaxInputTokens, p.MaxOutputTokens, p.CreatedAt,
                ContentHashShort: p.ContentHash is not null && p.ContentHash.Length >= 8
                    ? p.ContentHash[..8]
                    : p.ContentHash))
            .ToList();

        return new GenerationQualitySummary(
            totalFailures,
            abandonedCount,
            recentFailureCount,
            latestFailures,
            patternBreakdown,
            cefrBreakdown,
            promptSummary,
            providerBreakdown,
            abandonedWarning,
            retentionDays);
    }
}
