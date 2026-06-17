using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.UsageGovernance;

public sealed class UsageQuotaService : IUsageQuotaService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<UsageQuotaService> _logger;

    // Features available as alternatives when an expensive feature is blocked
    private static readonly IReadOnlyList<string> PreparedAlternatives = new[]
    {
        "lesson.view", "lesson.complete", "practice.prepared.complete", "tts.replay"
    };

    public UsageQuotaService(LinguaCoachDbContext db, ILogger<UsageQuotaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<QuotaDecision> CheckAsync(
        Guid studentProfileId,
        string featureKey,
        long estimatedUnits = 1,
        decimal? estimatedCost = null,
        CancellationToken ct = default)
    {
        var policy = await GetEffectivePolicyAsync(studentProfileId, ct);
        if (policy is null)
            return QuotaDecision.Allow(featureKey);

        var rule = policy.Rules.FirstOrDefault(r => r.FeatureKey == featureKey && r.IsActive);
        if (rule is null)
            return QuotaDecision.Allow(featureKey, EnforcementMode.None);

        if (rule.EnforcementMode == EnforcementMode.None || rule.EnforcementMode == EnforcementMode.TrackOnly)
            return QuotaDecision.Allow(featureKey, rule.EnforcementMode);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // Aggregate current usage from events (daily/weekly/monthly)
        var usedToday = await CountUsageAsync(studentProfileId, featureKey, today, today, rule.UnitType, ct);
        var usedThisWeek = await CountUsageAsync(studentProfileId, featureKey, weekStart, today, rule.UnitType, ct);
        var usedThisMonth = await CountUsageAsync(studentProfileId, featureKey, monthStart, today, rule.UnitType, ct);

        // Cost aggregates from daily aggregate table
        var (costToday, costMonth) = await GetCostAggregatesAsync(studentProfileId, today, monthStart, ct);

        // Check hard limits
        if (rule.EnforcementMode == EnforcementMode.HardLimit)
        {
            if (rule.DailyLimit.HasValue && usedToday + estimatedUnits > rule.DailyLimit.Value)
                return BuildBlocked(featureKey, rule, $"You have reached today's {GetFeatureFriendlyName(featureKey)} limit.", usedToday, usedThisWeek, usedThisMonth, costToday, costMonth);

            if (rule.WeeklyLimit.HasValue && usedThisWeek + estimatedUnits > rule.WeeklyLimit.Value)
                return BuildBlocked(featureKey, rule, $"You have reached this week's {GetFeatureFriendlyName(featureKey)} limit.", usedToday, usedThisWeek, usedThisMonth, costToday, costMonth);

            if (rule.MonthlyLimit.HasValue && usedThisMonth + estimatedUnits > rule.MonthlyLimit.Value)
                return BuildBlocked(featureKey, rule, $"You have reached this month's {GetFeatureFriendlyName(featureKey)} limit.", usedToday, usedThisWeek, usedThisMonth, costToday, costMonth);

            if (rule.DailyCostLimit.HasValue && estimatedCost.HasValue && costToday + estimatedCost.Value > rule.DailyCostLimit.Value)
                return BuildBlocked(featureKey, rule, $"Daily cost limit reached for {GetFeatureFriendlyName(featureKey)}.", usedToday, usedThisWeek, usedThisMonth, costToday, costMonth);

            if (rule.MonthlyCostLimit.HasValue && estimatedCost.HasValue && costMonth + estimatedCost.Value > rule.MonthlyCostLimit.Value)
                return BuildBlocked(featureKey, rule, $"Monthly cost limit reached for {GetFeatureFriendlyName(featureKey)}.", usedToday, usedThisWeek, usedThisMonth, costToday, costMonth);
        }

        return new QuotaDecision
        {
            Allowed = true,
            FeatureKey = featureKey,
            EnforcementMode = rule.EnforcementMode,
            UsedToday = usedToday,
            DailyLimit = rule.DailyLimit,
            UsedThisWeek = usedThisWeek,
            WeeklyLimit = rule.WeeklyLimit,
            UsedThisMonth = usedThisMonth,
            MonthlyLimit = rule.MonthlyLimit,
            UsedCostToday = costToday,
            DailyCostLimit = rule.DailyCostLimit,
            UsedCostThisMonth = costMonth,
            MonthlyCostLimit = rule.MonthlyCostLimit
        };
    }

    public async Task RecordAsync(UsageEvent usageEvent, CancellationToken ct = default)
    {
        _db.UsageEvents.Add(usageEvent);

        var today = DateOnly.FromDateTime(usageEvent.CreatedAt == default ? DateTime.UtcNow : usageEvent.CreatedAt);
        var aggregate = await _db.StudentUsageDaily
            .FirstOrDefaultAsync(a => a.StudentProfileId == usageEvent.StudentProfileId && a.Date == today, ct);

        if (aggregate is null)
        {
            aggregate = new StudentUsageDaily(usageEvent.StudentProfileId, today);
            _db.StudentUsageDaily.Add(aggregate);
        }

        bool isAiCall = usageEvent.TotalTokens > 0 || !string.IsNullOrEmpty(usageEvent.Provider);

        aggregate.Apply(
            usageEvent.InputTokens,
            usageEvent.OutputTokens,
            usageEvent.TotalTokens,
            usageEvent.EstimatedCost ?? 0m,
            isAiCall,
            liveAiMinutes: 0m,
            usageEvent.TtsCharacters ?? 0,
            usageEvent.SttMinutes ?? 0m,
            usageEvent.FeatureKey);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<UsageSummary> GetUsageSummaryAsync(
        Guid studentProfileId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        var rows = await _db.StudentUsageDaily
            .Where(d => d.StudentProfileId == studentProfileId && d.Date >= from && d.Date <= to)
            .ToListAsync(ct);

        return new UsageSummary
        {
            StudentProfileId = studentProfileId,
            From = from,
            To = to,
            Period = from == to ? "day" : "range",
            TotalTokens = rows.Sum(r => r.TotalTokens),
            InputTokens = rows.Sum(r => r.InputTokens),
            OutputTokens = rows.Sum(r => r.OutputTokens),
            TotalCost = rows.Sum(r => r.TotalCost),
            AiCallCount = rows.Sum(r => r.AiCallCount),
            LessonGenerations = rows.Sum(r => r.LessonGenerations),
            PracticeGenerations = rows.Sum(r => r.PracticeGenerations),
            WritingEvaluations = rows.Sum(r => r.WritingEvaluations),
            SpeakingEvaluations = rows.Sum(r => r.SpeakingEvaluations),
            PreparedActivitiesCompleted = rows.Sum(r => r.PreparedActivitiesCompleted)
        };
    }

    public async Task<UsagePolicy?> GetEffectivePolicyAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        // Student-specific override takes priority over global default
        var assignment = await _db.StudentPolicyAssignments
            .Where(a => a.StudentProfileId == studentProfileId && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        Guid? policyId = assignment?.UsagePolicyId;

        if (policyId is null)
        {
            // Fall back to global default
            policyId = await _db.UsagePolicies
                .Where(p => p.IsDefault && p.IsActive && p.ScopeType == UsagePolicyScopeType.Global)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (policyId is null)
            return null;

        return await _db.UsagePolicies
            .Include(p => p.Rules.Where(r => r.IsActive))
            .FirstOrDefaultAsync(p => p.Id == policyId, ct);
    }

    // --- helpers ---

    private async Task<long> CountUsageAsync(
        Guid studentProfileId,
        string featureKey,
        DateOnly from,
        DateOnly to,
        UsageUnitType unitType,
        CancellationToken ct)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var events = await _db.UsageEvents
            .Where(e => e.StudentProfileId == studentProfileId
                     && e.FeatureKey == featureKey
                     && e.Success
                     && e.CreatedAt >= fromUtc
                     && e.CreatedAt <= toUtc)
            .ToListAsync(ct);

        return unitType switch
        {
            UsageUnitType.InputTokens => events.Sum(e => (long)e.InputTokens),
            UsageUnitType.OutputTokens => events.Sum(e => (long)e.OutputTokens),
            UsageUnitType.Tokens => events.Sum(e => (long)e.TotalTokens),
            UsageUnitType.Characters => events.Sum(e => (long)(e.TtsCharacters ?? 0)),
            UsageUnitType.Minutes => (long)events.Sum(e => e.SttMinutes ?? 0m),
            UsageUnitType.Seconds => (long)events.Sum(e => e.AudioSeconds ?? 0m),
            _ => events.Sum(e => e.UnitsUsed)
        };
    }

    private async Task<(decimal today, decimal month)> GetCostAggregatesAsync(
        Guid studentProfileId,
        DateOnly today,
        DateOnly monthStart,
        CancellationToken ct)
    {
        var rows = await _db.StudentUsageDaily
            .Where(d => d.StudentProfileId == studentProfileId && d.Date >= monthStart && d.Date <= today)
            .ToListAsync(ct);

        var todayCost = rows.Where(r => r.Date == today).Sum(r => r.TotalCost);
        var monthCost = rows.Sum(r => r.TotalCost);
        return (todayCost, monthCost);
    }

    private static QuotaDecision BuildBlocked(
        string featureKey,
        UsagePolicyRule rule,
        string reason,
        long usedToday,
        long usedThisWeek,
        long usedThisMonth,
        decimal costToday,
        decimal costMonth)
    {
        return new QuotaDecision
        {
            Allowed = false,
            FeatureKey = featureKey,
            EnforcementMode = rule.EnforcementMode,
            Reason = reason,
            UsedToday = usedToday,
            DailyLimit = rule.DailyLimit,
            UsedThisWeek = usedThisWeek,
            WeeklyLimit = rule.WeeklyLimit,
            UsedThisMonth = usedThisMonth,
            MonthlyLimit = rule.MonthlyLimit,
            UsedCostToday = costToday,
            DailyCostLimit = rule.DailyCostLimit,
            UsedCostThisMonth = costMonth,
            MonthlyCostLimit = rule.MonthlyCostLimit,
            ResetAt = DateTime.UtcNow.Date.AddDays(1),
            AvailableAlternatives = PreparedAlternatives
        };
    }

    private static string GetFeatureFriendlyName(string featureKey) => featureKey switch
    {
        "writing.evaluate" => "writing evaluation",
        "speaking.evaluate" => "speaking evaluation",
        "speaking.live_session" => "live speaking session",
        "tts.generate" => "text-to-speech generation",
        "stt.transcribe" => "speech transcription",
        "lesson.generate" => "lesson generation",
        "lesson.regenerate" => "lesson regeneration",
        "practice.dynamic.generate" => "dynamic practice generation",
        "learning_path.generate" => "learning path generation",
        "learning_path.regenerate" => "learning path regeneration",
        _ => featureKey
    };
}
