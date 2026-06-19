using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.UsageGovernance;

public sealed class UsageGovernanceAdminService : IUsageGovernanceAdminService
{
    private readonly LinguaCoachDbContext _db;

    public UsageGovernanceAdminService(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeatureDefinition>> ListFeatureDefinitionsAsync(CancellationToken ct = default) =>
        await _db.FeatureDefinitions.OrderBy(f => f.Category).ThenBy(f => f.Key).ToListAsync(ct);

    public async Task<IReadOnlyList<UsagePolicy>> ListUsagePoliciesAsync(CancellationToken ct = default) =>
        await _db.UsagePolicies
            .Include(p => p.Rules.Where(r => r.IsActive))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<UsagePolicy?> GetUsagePolicyAsync(Guid id, CancellationToken ct = default) =>
        await _db.UsagePolicies
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<UsagePolicy> CreateUsagePolicyAsync(
        CreateUsagePolicyRequest request,
        Guid adminUserId,
        CancellationToken ct = default)
    {
        var policy = new UsagePolicy(
            request.Name,
            request.Description,
            request.ScopeType,
            request.IsDefault,
            request.IsActive);

        _db.UsagePolicies.Add(policy);
        await _db.SaveChangesAsync(ct);

        foreach (var r in request.Rules)
        {
            _db.UsagePolicyRules.Add(new UsagePolicyRule(
                policy.Id, r.FeatureKey, r.TrackingEnabled,
                r.EnforcementMode, r.UnitType,
                r.DailyLimit, r.WeeklyLimit, r.MonthlyLimit,
                r.DailyCostLimit, r.MonthlyCostLimit,
                r.WarningThresholdPercent, r.IsActive));
        }

        await _db.SaveChangesAsync(ct);
        return policy;
    }

    public async Task<UsagePolicy> UpdateUsagePolicyAsync(
        Guid id,
        UpdateUsagePolicyRequest request,
        Guid adminUserId,
        CancellationToken ct = default)
    {
        var policy = await _db.UsagePolicies.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Usage policy {id} not found.");

        policy.Update(request.Name, request.Description, request.IsDefault, request.IsActive);
        await _db.SaveChangesAsync(ct);
        return policy;
    }

    public async Task<UsagePolicyRule> AddRuleAsync(
        Guid policyId,
        AddUsagePolicyRuleRequest request,
        Guid adminUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FeatureKey))
            throw new ArgumentException("Feature key is required.", nameof(request));

        var policy = await _db.UsagePolicies.FirstOrDefaultAsync(p => p.Id == policyId, ct)
            ?? throw new KeyNotFoundException($"Usage policy {policyId} not found.");

        var existing = await _db.UsagePolicyRules
            .FirstOrDefaultAsync(r => r.UsagePolicyId == policyId
                && r.FeatureKey == request.FeatureKey.Trim().ToLowerInvariant(), ct);
        if (existing is not null)
            throw new InvalidOperationException(
                $"A rule for feature '{request.FeatureKey}' already exists in this policy.");

        var rule = new UsagePolicyRule(
            policy.Id, request.FeatureKey, request.TrackingEnabled,
            request.EnforcementMode, request.UnitType,
            request.DailyLimit, request.WeeklyLimit, request.MonthlyLimit,
            request.DailyCostLimit, request.MonthlyCostLimit,
            request.WarningThresholdPercent, request.IsActive);

        _db.UsagePolicyRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<UsagePolicyRule> UpdateRuleAsync(
        Guid policyId,
        Guid ruleId,
        UpdateUsagePolicyRuleRequest request,
        Guid adminUserId,
        CancellationToken ct = default)
    {
        var rule = await _db.UsagePolicyRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.UsagePolicyId == policyId, ct)
            ?? throw new KeyNotFoundException($"Rule {ruleId} not found in policy {policyId}.");

        rule.Update(
            request.TrackingEnabled, request.EnforcementMode, request.UnitType,
            request.DailyLimit, request.WeeklyLimit, request.MonthlyLimit,
            request.DailyCostLimit, request.MonthlyCostLimit,
            request.WarningThresholdPercent, request.IsActive);

        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task DeleteRuleAsync(
        Guid policyId,
        Guid ruleId,
        Guid adminUserId,
        CancellationToken ct = default)
    {
        var rule = await _db.UsagePolicyRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.UsagePolicyId == policyId, ct)
            ?? throw new KeyNotFoundException($"Rule {ruleId} not found in policy {policyId}.");

        _db.UsagePolicyRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssignPolicyToStudentAsync(
        Guid studentProfileId,
        Guid usagePolicyId,
        Guid adminUserId,
        string? reason,
        CancellationToken ct = default)
    {
        // Deactivate any existing assignment
        var existing = await _db.StudentPolicyAssignments
            .Where(a => a.StudentProfileId == studentProfileId && a.IsActive)
            .ToListAsync(ct);

        foreach (var a in existing)
            a.Deactivate();

        var policy = await _db.UsagePolicies.FirstOrDefaultAsync(p => p.Id == usagePolicyId && p.IsActive, ct)
            ?? throw new KeyNotFoundException($"Usage policy {usagePolicyId} not found or inactive.");

        _db.StudentPolicyAssignments.Add(
            new StudentPolicyAssignment(studentProfileId, usagePolicyId, adminUserId, reason));

        _db.AdminAuditLogs.Add(new AdminAuditLog(
            adminUserId, "AssignUsagePolicy", "StudentPolicyAssignment",
            entityId: studentProfileId.ToString(),
            targetStudentId: studentProfileId,
            newValueJson: $"{{\"policyId\":\"{usagePolicyId}\",\"policyName\":\"{policy.Name}\"}}",
            reason: reason));

        await _db.SaveChangesAsync(ct);
    }

    public async Task<UsagePolicy?> GetStudentEffectivePolicyAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var assignment = await _db.StudentPolicyAssignments
            .Where(a => a.StudentProfileId == studentProfileId && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        Guid? policyId = assignment?.UsagePolicyId;

        if (policyId is null)
        {
            policyId = await _db.UsagePolicies
                .Where(p => p.IsDefault && p.IsActive && p.ScopeType == UsagePolicyScopeType.Global)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (policyId is null) return null;

        return await _db.UsagePolicies
            .Include(p => p.Rules.Where(r => r.IsActive))
            .FirstOrDefaultAsync(p => p.Id == policyId, ct);
    }
}
