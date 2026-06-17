using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

public sealed class UsagePolicyRule : BaseEntity
{
    public Guid UsagePolicyId { get; private set; }
    public UsagePolicy? UsagePolicy { get; private set; }

    public string FeatureKey { get; private set; }
    public bool TrackingEnabled { get; private set; }
    public EnforcementMode EnforcementMode { get; private set; }
    public UsageUnitType UnitType { get; private set; }

    public long? DailyLimit { get; private set; }
    public long? WeeklyLimit { get; private set; }
    public long? MonthlyLimit { get; private set; }
    public decimal? DailyCostLimit { get; private set; }
    public decimal? MonthlyCostLimit { get; private set; }
    public int WarningThresholdPercent { get; private set; }
    public bool IsActive { get; private set; }

    private UsagePolicyRule()
    {
        FeatureKey = string.Empty;
    }

    public UsagePolicyRule(
        Guid usagePolicyId,
        string featureKey,
        bool trackingEnabled,
        EnforcementMode enforcementMode,
        UsageUnitType unitType,
        long? dailyLimit,
        long? weeklyLimit,
        long? monthlyLimit,
        decimal? dailyCostLimit,
        decimal? monthlyCostLimit,
        int warningThresholdPercent,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) throw new ArgumentException("Feature key is required.", nameof(featureKey));
        if (dailyLimit.HasValue && dailyLimit < 0) throw new ArgumentOutOfRangeException(nameof(dailyLimit), "Daily limit cannot be negative.");
        if (weeklyLimit.HasValue && weeklyLimit < 0) throw new ArgumentOutOfRangeException(nameof(weeklyLimit), "Weekly limit cannot be negative.");
        if (monthlyLimit.HasValue && monthlyLimit < 0) throw new ArgumentOutOfRangeException(nameof(monthlyLimit), "Monthly limit cannot be negative.");
        if (dailyCostLimit.HasValue && dailyCostLimit < 0) throw new ArgumentOutOfRangeException(nameof(dailyCostLimit), "Daily cost limit cannot be negative.");
        if (monthlyCostLimit.HasValue && monthlyCostLimit < 0) throw new ArgumentOutOfRangeException(nameof(monthlyCostLimit), "Monthly cost limit cannot be negative.");
        if (warningThresholdPercent < 0 || warningThresholdPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(warningThresholdPercent), "Warning threshold must be 0-100.");

        UsagePolicyId = usagePolicyId;
        FeatureKey = featureKey.Trim().ToLowerInvariant();
        TrackingEnabled = trackingEnabled;
        EnforcementMode = enforcementMode;
        UnitType = unitType;
        DailyLimit = dailyLimit;
        WeeklyLimit = weeklyLimit;
        MonthlyLimit = monthlyLimit;
        DailyCostLimit = dailyCostLimit;
        MonthlyCostLimit = monthlyCostLimit;
        WarningThresholdPercent = warningThresholdPercent;
        IsActive = isActive;
    }
}
