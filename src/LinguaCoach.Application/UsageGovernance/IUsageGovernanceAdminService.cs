using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.UsageGovernance;

public interface IUsageGovernanceAdminService
{
    // Feature definitions
    Task<IReadOnlyList<FeatureDefinition>> ListFeatureDefinitionsAsync(CancellationToken ct = default);

    // Usage policies
    Task<IReadOnlyList<UsagePolicy>> ListUsagePoliciesAsync(CancellationToken ct = default);
    Task<UsagePolicy?> GetUsagePolicyAsync(Guid id, CancellationToken ct = default);
    Task<UsagePolicy> CreateUsagePolicyAsync(CreateUsagePolicyRequest request, Guid adminUserId, CancellationToken ct = default);
    Task<UsagePolicy> UpdateUsagePolicyAsync(Guid id, UpdateUsagePolicyRequest request, Guid adminUserId, CancellationToken ct = default);

    // Student policy assignment
    Task AssignPolicyToStudentAsync(Guid studentProfileId, Guid usagePolicyId, Guid adminUserId, string? reason, CancellationToken ct = default);
    Task<UsagePolicy?> GetStudentEffectivePolicyAsync(Guid studentProfileId, CancellationToken ct = default);
}

public sealed record CreateUsagePolicyRequest(
    string Name,
    string? Description,
    UsagePolicyScopeType ScopeType,
    bool IsDefault,
    bool IsActive,
    IReadOnlyList<CreateUsagePolicyRuleRequest> Rules);

public sealed record UpdateUsagePolicyRequest(
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive);

public sealed record CreateUsagePolicyRuleRequest(
    string FeatureKey,
    bool TrackingEnabled,
    EnforcementMode EnforcementMode,
    UsageUnitType UnitType,
    long? DailyLimit,
    long? WeeklyLimit,
    long? MonthlyLimit,
    decimal? DailyCostLimit,
    decimal? MonthlyCostLimit,
    int WarningThresholdPercent = 80,
    bool IsActive = true);
