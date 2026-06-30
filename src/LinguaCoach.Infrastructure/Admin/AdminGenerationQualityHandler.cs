using LinguaCoach.Application.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminGenerationQualityHandler : IAdminGenerationQualityHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminGenerationQualityHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<GenerationQualitySummary> GetSummaryAsync(int recentDays = 30, CancellationToken ct = default)
    {
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
                f.AttemptNumber))
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

        var prompts = await _db.AiPrompts
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Key)
            .ToListAsync(ct);

        var promptSummary = prompts
            .Select(p => new PromptTemplateItem(
                p.Id, p.Key, p.Version, p.IsActive, p.MaxInputTokens, p.MaxOutputTokens, p.CreatedAt))
            .ToList();

        return new GenerationQualitySummary(
            totalFailures,
            abandonedCount,
            recentFailureCount,
            latestFailures,
            patternBreakdown,
            cefrBreakdown,
            promptSummary);
    }
}
