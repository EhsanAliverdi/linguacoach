using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.UsageGovernance;

public sealed class QuotaDecision
{
    public bool Allowed { get; init; }
    public string FeatureKey { get; init; } = string.Empty;
    public EnforcementMode EnforcementMode { get; init; }
    public string? Reason { get; init; }

    // Daily usage
    public long UsedToday { get; init; }
    public long? DailyLimit { get; init; }

    // Weekly usage
    public long UsedThisWeek { get; init; }
    public long? WeeklyLimit { get; init; }

    // Monthly usage
    public long UsedThisMonth { get; init; }
    public long? MonthlyLimit { get; init; }

    // Cost usage
    public decimal UsedCostToday { get; init; }
    public decimal? DailyCostLimit { get; init; }
    public decimal UsedCostThisMonth { get; init; }
    public decimal? MonthlyCostLimit { get; init; }

    public DateTime? ResetAt { get; init; }

    /// <summary>
    /// Feature keys the student can still use as alternatives.
    /// </summary>
    public IReadOnlyList<string> AvailableAlternatives { get; init; } = Array.Empty<string>();

    public static QuotaDecision Allow(string featureKey, EnforcementMode mode = EnforcementMode.TrackOnly) =>
        new() { Allowed = true, FeatureKey = featureKey, EnforcementMode = mode };

    public static QuotaDecision Block(string featureKey, string reason, EnforcementMode mode, IReadOnlyList<string>? alternatives = null) =>
        new()
        {
            Allowed = false,
            FeatureKey = featureKey,
            EnforcementMode = mode,
            Reason = reason,
            AvailableAlternatives = alternatives ?? Array.Empty<string>()
        };
}
