using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.UsageGovernance;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinguaCoach.IntegrationTests.UsageGovernance;

/// <summary>
/// Phase 10R — Usage governance integration tests (20 tests).
/// DB-layer tests use their own in-memory SQLite connection for isolation.
/// HTTP endpoint tests use ApiTestFactory via IClassFixture.
/// </summary>
public sealed class UsageGovernanceDbTests : IAsyncLifetime
{
    private SqliteConnection? _connection;
    private LinguaCoachDbContext? _db;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LinguaCoachDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await LinguaCoach.Persistence.Seed.UsageGovernanceSeeder.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        if (_db is not null) await _db.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private LinguaCoachDbContext Db => _db!;

    private IUsageQuotaService Quota => new UsageQuotaService(
        Db, NullLogger<UsageQuotaService>.Instance);

    private IUsageGovernanceAdminService AdminSvc => new UsageGovernanceAdminService(Db);

    private async Task<Guid> CreateStudentAsync()
    {
        var userId = Guid.NewGuid();
        var profile = new LinguaCoach.Domain.Entities.StudentProfile(userId);
        Db.StudentProfiles.Add(profile);
        await Db.SaveChangesAsync();
        return profile.Id;
    }

    // ── 1. Seeding is idempotent ──────────────────────────────────────────────

    [Fact]
    public async Task FeatureDefinitions_SeededIdempotently()
    {
        var beforeCount = await Db.FeatureDefinitions.CountAsync();
        await LinguaCoach.Persistence.Seed.UsageGovernanceSeeder.SeedAsync(Db);
        var afterCount = await Db.FeatureDefinitions.CountAsync();
        Assert.Equal(beforeCount, afterCount);
    }

    [Fact]
    public async Task UsagePolicies_SeededIdempotently()
    {
        var beforeCount = await Db.UsagePolicies.CountAsync();
        await LinguaCoach.Persistence.Seed.UsageGovernanceSeeder.SeedAsync(Db);
        var afterCount = await Db.UsagePolicies.CountAsync();
        Assert.Equal(beforeCount, afterCount);
    }

    [Fact]
    public async Task PolicyRules_SeededIdempotently()
    {
        var beforeCount = await Db.UsagePolicyRules.CountAsync();
        await LinguaCoach.Persistence.Seed.UsageGovernanceSeeder.SeedAsync(Db);
        var afterCount = await Db.UsagePolicyRules.CountAsync();
        Assert.Equal(beforeCount, afterCount);
    }

    // ── 2. Quota service: record & check ──────────────────────────────────────

    [Fact]
    public async Task QuotaService_RecordAsync_CreatesUsageEventAndAggregate()
    {
        var studentId = await CreateStudentAsync();
        var ev = new UsageEvent(studentId, "writing.evaluate", UsageUnitType.Count, 1,
            "openai", "gpt-4", 200, 100, 300, 0.02m, null, "corr-1", true);

        await Quota.RecordAsync(ev);

        var eventCount = await Db.UsageEvents.CountAsync(e => e.StudentProfileId == studentId);
        var aggregate = await Db.StudentUsageDaily.FirstOrDefaultAsync(d => d.StudentProfileId == studentId);

        Assert.Equal(1, eventCount);
        Assert.NotNull(aggregate);
        Assert.Equal(300, aggregate.TotalTokens);
        Assert.Equal(0.02m, aggregate.TotalCost);
    }

    [Fact]
    public async Task QuotaService_TokenUsagePersisted()
    {
        var studentId = await CreateStudentAsync();
        var ev = new UsageEvent(studentId, "speaking.evaluate", UsageUnitType.Count, 1,
            "anthropic", "claude-sonnet-4-6", 500, 300, 800, 0.05m, "req-abc", "corr-2", true);

        await Quota.RecordAsync(ev);

        var stored = await Db.UsageEvents.FirstAsync(e => e.StudentProfileId == studentId);
        Assert.Equal(500, stored.InputTokens);
        Assert.Equal(300, stored.OutputTokens);
        Assert.Equal(800, stored.TotalTokens);
        Assert.Equal("anthropic", stored.Provider);
        Assert.Equal("claude-sonnet-4-6", stored.Model);
        Assert.Equal("corr-2", stored.CorrelationId);
    }

    [Fact]
    public async Task QuotaService_ProviderAndModelPersisted()
    {
        var studentId = await CreateStudentAsync();
        var ev = new UsageEvent(studentId, "lesson.generate", UsageUnitType.Count, 1,
            "openai", "gpt-4o", 300, 400, 700, 0.03m, "req-xyz", "corr-xyz", true);

        await Quota.RecordAsync(ev);

        var stored = await Db.UsageEvents.FirstAsync(e => e.StudentProfileId == studentId);
        Assert.Equal("openai", stored.Provider);
        Assert.Equal("gpt-4o", stored.Model);
        Assert.Equal("req-xyz", stored.RequestId);
    }

    [Fact]
    public async Task QuotaService_HardLimit_BlocksWhenDailyLimitExceeded()
    {
        var studentId = await CreateStudentAsync();
        var lowCostPolicy = await Db.UsagePolicies.FirstAsync(p => p.Name == "Low Cost Student");
        Db.StudentPolicyAssignments.Add(
            new StudentPolicyAssignment(studentId, lowCostPolicy.Id, Guid.NewGuid(), "test"));
        await Db.SaveChangesAsync();

        for (int i = 0; i < 5; i++)
        {
            var ev = new UsageEvent(studentId, "writing.evaluate", UsageUnitType.Count, 1,
                null, null, 0, 0, 0, null, null, null, true);
            await Quota.RecordAsync(ev);
        }

        var decision = await Quota.CheckAsync(studentId, "writing.evaluate", estimatedUnits: 1);
        Assert.False(decision.Allowed);
        Assert.Equal(EnforcementMode.HardLimit, decision.EnforcementMode);
    }

    [Fact]
    public async Task QuotaService_TrackOnly_AllowsEvenOverLimit()
    {
        var studentId = await CreateStudentAsync();
        // Default policy is TrackOnly — no hard limits
        var decision = await Quota.CheckAsync(studentId, "writing.evaluate", estimatedUnits: 999);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task QuotaService_PreparedLearning_AllowedDespiteOtherLimit()
    {
        var studentId = await CreateStudentAsync();
        var lowCostPolicy = await Db.UsagePolicies.FirstAsync(p => p.Name == "Low Cost Student");
        Db.StudentPolicyAssignments.Add(
            new StudentPolicyAssignment(studentId, lowCostPolicy.Id, Guid.NewGuid(), "test"));
        await Db.SaveChangesAsync();

        var decision = await Quota.CheckAsync(studentId, "practice.prepared.complete", estimatedUnits: 1);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task QuotaDecision_BlockedResponse_IncludesAlternatives()
    {
        var studentId = await CreateStudentAsync();
        var lowCostPolicy = await Db.UsagePolicies.FirstAsync(p => p.Name == "Low Cost Student");
        Db.StudentPolicyAssignments.Add(
            new StudentPolicyAssignment(studentId, lowCostPolicy.Id, Guid.NewGuid(), "test"));
        await Db.SaveChangesAsync();

        for (int i = 0; i < 5; i++)
        {
            await Quota.RecordAsync(new UsageEvent(studentId, "writing.evaluate", UsageUnitType.Count, 1,
                null, null, 0, 0, 0, null, null, null, true));
        }

        var decision = await Quota.CheckAsync(studentId, "writing.evaluate");
        Assert.False(decision.Allowed);
        Assert.NotEmpty(decision.AvailableAlternatives);
    }

    // ── 3. Audit log ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignPolicyToStudent_WritesAuditLog()
    {
        var studentId = await CreateStudentAsync();
        var policy = await Db.UsagePolicies.FirstAsync(p => p.IsActive);
        var adminId = Guid.NewGuid();

        await AdminSvc.AssignPolicyToStudentAsync(studentId, policy.Id, adminId, "test reason");

        var log = await Db.AdminAuditLogs.FirstOrDefaultAsync(l => l.TargetStudentId == studentId);
        Assert.NotNull(log);
        Assert.Equal("AssignUsagePolicy", log.Action);
        Assert.Equal(adminId, log.ActorAdminUserId);
    }

    // ── 4. Effective policy resolution ───────────────────────────────────────

    [Fact]
    public async Task GetEffectivePolicy_NoAssignment_ReturnsGlobalDefault()
    {
        var studentId = await CreateStudentAsync();
        var policy = await Quota.GetEffectivePolicyAsync(studentId);
        Assert.NotNull(policy);
        Assert.True(policy.IsDefault);
    }

    [Fact]
    public async Task GetEffectivePolicy_StudentAssignment_OverridesDefault()
    {
        var studentId = await CreateStudentAsync();
        var overridePolicy = await Db.UsagePolicies.FirstAsync(p => p.Name == "Test Unlimited");
        await AdminSvc.AssignPolicyToStudentAsync(studentId, overridePolicy.Id, Guid.NewGuid(), null);

        var effective = await Quota.GetEffectivePolicyAsync(studentId);
        Assert.Equal("Test Unlimited", effective!.Name);
    }

    // ── 5. Daily aggregate ────────────────────────────────────────────────────

    [Fact]
    public async Task DailyAggregate_MultipleEvents_AccumulatesCorrectly()
    {
        var studentId = await CreateStudentAsync();
        await Quota.RecordAsync(new UsageEvent(studentId, "writing.evaluate", UsageUnitType.Count, 1,
            "openai", "gpt-4", 100, 200, 300, 0.01m, null, null, true));
        await Quota.RecordAsync(new UsageEvent(studentId, "speaking.evaluate", UsageUnitType.Count, 1,
            "openai", "gpt-4", 150, 250, 400, 0.02m, null, null, true));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var aggregate = await Db.StudentUsageDaily.FirstAsync(d => d.StudentProfileId == studentId && d.Date == today);

        Assert.Equal(700, aggregate.TotalTokens);
        Assert.Equal(0.03m, aggregate.TotalCost);
        Assert.Equal(2, aggregate.AiCallCount);
        Assert.Equal(1, aggregate.WritingEvaluations);
        Assert.Equal(1, aggregate.SpeakingEvaluations);
    }

    // ── 6. Usage summary ──────────────────────────────────────────────────────

    [Fact]
    public async Task UsageSummary_ReturnsCorrectAggregatedTotals()
    {
        var studentId = await CreateStudentAsync();
        await Quota.RecordAsync(new UsageEvent(studentId, "lesson.generate", UsageUnitType.Count, 1,
            "anthropic", "claude-sonnet-4-6", 300, 200, 500, 0.04m, null, null, true));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await Quota.GetUsageSummaryAsync(studentId, today, today);

        Assert.Equal(500, summary.TotalTokens);
        Assert.Equal(0.04m, summary.TotalCost);
        Assert.Equal(1, summary.AiCallCount);
        Assert.Equal(1, summary.LessonGenerations);
    }

    // ── 7. Admin service: policy CRUD ─────────────────────────────────────────

    [Fact]
    public async Task AdminService_CreateAndUpdatePolicy_Succeeds()
    {
        var policy = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest(
                "Test CRUD Policy", "desc", UsagePolicyScopeType.Student,
                false, true, []),
            Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, policy.Id);

        var updated = await AdminSvc.UpdateUsagePolicyAsync(
            policy.Id,
            new UpdateUsagePolicyRequest("Renamed Policy", "new desc", false, true),
            Guid.NewGuid());

        Assert.Equal("Renamed Policy", updated.Name);
    }

    // ── 8. Admin service: rule CRUD ───────────────────────────────────────────

    [Fact]
    public async Task AdminService_AddRule_CreatesRule()
    {
        var policy = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Rule Add Policy", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());

        var rule = await AdminSvc.AddRuleAsync(policy.Id,
            new AddUsagePolicyRuleRequest("writing.evaluate", true, EnforcementMode.HardLimit,
                UsageUnitType.Count, DailyLimit: 5, null, null, null, null),
            Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, rule.Id);
        Assert.Equal("writing.evaluate", rule.FeatureKey);
        Assert.Equal(EnforcementMode.HardLimit, rule.EnforcementMode);
        Assert.Equal(5, rule.DailyLimit);

        var inDb = await Db.UsagePolicyRules.FindAsync(rule.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task AdminService_AddRule_DuplicateFeatureKey_Throws()
    {
        var policy = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Dup Rule Policy", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());

        await AdminSvc.AddRuleAsync(policy.Id,
            new AddUsagePolicyRuleRequest("speaking.evaluate", true, EnforcementMode.TrackOnly,
                UsageUnitType.Count, null, null, null, null, null),
            Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AdminSvc.AddRuleAsync(policy.Id,
                new AddUsagePolicyRuleRequest("speaking.evaluate", false, EnforcementMode.HardLimit,
                    UsageUnitType.Count, null, null, null, null, null),
                Guid.NewGuid()));
    }

    [Fact]
    public async Task AdminService_AddRule_PolicyNotFound_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            AdminSvc.AddRuleAsync(Guid.NewGuid(),
                new AddUsagePolicyRuleRequest("writing.evaluate", true, EnforcementMode.TrackOnly,
                    UsageUnitType.Count, null, null, null, null, null),
                Guid.NewGuid()));
    }

    [Fact]
    public async Task AdminService_UpdateRule_ChangesFields()
    {
        var policy = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Rule Update Policy", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());

        var rule = await AdminSvc.AddRuleAsync(policy.Id,
            new AddUsagePolicyRuleRequest("writing.evaluate", true, EnforcementMode.TrackOnly,
                UsageUnitType.Count, null, null, null, null, null),
            Guid.NewGuid());

        var updated = await AdminSvc.UpdateRuleAsync(policy.Id, rule.Id,
            new UpdateUsagePolicyRuleRequest(false, EnforcementMode.HardLimit, UsageUnitType.Tokens,
                DailyLimit: 10, null, MonthlyLimit: 50, null, null, WarningThresholdPercent: 70, IsActive: true),
            Guid.NewGuid());

        Assert.Equal(EnforcementMode.HardLimit, updated.EnforcementMode);
        Assert.Equal(UsageUnitType.Tokens, updated.UnitType);
        Assert.Equal(10, updated.DailyLimit);
        Assert.Equal(50, updated.MonthlyLimit);
        Assert.Equal(70, updated.WarningThresholdPercent);
        Assert.False(updated.TrackingEnabled);
    }

    [Fact]
    public async Task AdminService_UpdateRule_WrongPolicy_Throws()
    {
        var policy1 = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Policy One", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());
        var policy2 = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Policy Two", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());

        var rule = await AdminSvc.AddRuleAsync(policy1.Id,
            new AddUsagePolicyRuleRequest("writing.evaluate", true, EnforcementMode.TrackOnly,
                UsageUnitType.Count, null, null, null, null, null),
            Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            AdminSvc.UpdateRuleAsync(policy2.Id, rule.Id,
                new UpdateUsagePolicyRuleRequest(true, EnforcementMode.TrackOnly, UsageUnitType.Count,
                    null, null, null, null, null),
                Guid.NewGuid()));
    }

    [Fact]
    public async Task AdminService_DeleteRule_RemovesFromDb()
    {
        var policy = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Rule Delete Policy", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());

        var rule = await AdminSvc.AddRuleAsync(policy.Id,
            new AddUsagePolicyRuleRequest("writing.evaluate", true, EnforcementMode.TrackOnly,
                UsageUnitType.Count, null, null, null, null, null),
            Guid.NewGuid());

        await AdminSvc.DeleteRuleAsync(policy.Id, rule.Id, Guid.NewGuid());

        var inDb = await Db.UsagePolicyRules.FindAsync(rule.Id);
        Assert.Null(inDb);
    }

    [Fact]
    public async Task AdminService_DeleteRule_WrongPolicy_Throws()
    {
        var policy1 = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Del Policy One", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());
        var policy2 = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Del Policy Two", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());

        var rule = await AdminSvc.AddRuleAsync(policy1.Id,
            new AddUsagePolicyRuleRequest("writing.evaluate", true, EnforcementMode.TrackOnly,
                UsageUnitType.Count, null, null, null, null, null),
            Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            AdminSvc.DeleteRuleAsync(policy2.Id, rule.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task AdminService_EnableDisableRule_ViaUpdate()
    {
        var policy = await AdminSvc.CreateUsagePolicyAsync(
            new CreateUsagePolicyRequest("Enable Disable Policy", null, UsagePolicyScopeType.Student, false, true, []),
            Guid.NewGuid());

        var rule = await AdminSvc.AddRuleAsync(policy.Id,
            new AddUsagePolicyRuleRequest("writing.evaluate", true, EnforcementMode.TrackOnly,
                UsageUnitType.Count, null, null, null, null, null, IsActive: true),
            Guid.NewGuid());

        // Disable
        var disabled = await AdminSvc.UpdateRuleAsync(policy.Id, rule.Id,
            new UpdateUsagePolicyRuleRequest(rule.TrackingEnabled, rule.EnforcementMode, rule.UnitType,
                rule.DailyLimit, rule.WeeklyLimit, rule.MonthlyLimit,
                rule.DailyCostLimit, rule.MonthlyCostLimit, rule.WarningThresholdPercent, IsActive: false),
            Guid.NewGuid());
        Assert.False(disabled.IsActive);

        // Re-enable
        var enabled = await AdminSvc.UpdateRuleAsync(policy.Id, rule.Id,
            new UpdateUsagePolicyRuleRequest(rule.TrackingEnabled, rule.EnforcementMode, rule.UnitType,
                rule.DailyLimit, rule.WeeklyLimit, rule.MonthlyLimit,
                rule.DailyCostLimit, rule.MonthlyCostLimit, rule.WarningThresholdPercent, IsActive: true),
            Guid.NewGuid());
        Assert.True(enabled.IsActive);
    }
}

/// <summary>
/// HTTP endpoint tests for admin usage governance APIs.
/// Uses a dedicated factory subclass to avoid fixture sharing conflicts.
/// </summary>
public sealed class UsageGovernanceEndpointTests : IClassFixture<UsageGovernanceTestFactory>
{
    private readonly UsageGovernanceTestFactory _factory;

    public UsageGovernanceEndpointTests(UsageGovernanceTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_ListFeatureDefinitions_ReturnsSeededDefinitions()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/admin/feature-definitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("writing.evaluate", body);
    }

    [Fact]
    public async Task Admin_ListFeatureDefinitions_NonAdmin_Returns403()
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync("govtest1ep@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var response = await client.GetAsync("/api/admin/feature-definitions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CreateUsagePolicy_Succeeds()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var payload = new
        {
            name = "EP Test Policy " + Guid.NewGuid().ToString("N")[..6],
            description = "Endpoint test policy",
            scopeType = "Student",
            isDefault = false,
            isActive = true,
            rules = Array.Empty<object>()
        };

        var response = await client.PostAsJsonAsync("/api/admin/usage-policies", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Admin_GetStudentUsage_ReturnsOk()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync("epusage@test.com");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

        var resp = await client.GetAsync($"/api/admin/students/{profile.Id}/usage?period=today");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_NonAdmin_CannotAssignPolicy_Returns403()
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync("nonadminep@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var payload = new { policyId = Guid.NewGuid(), reason = "" };
        var resp = await client.PutAsJsonAsync($"/api/admin/students/{Guid.NewGuid()}/usage-policy", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Rule CRUD endpoints ───────────────────────────────────────────────────

    private async Task<(System.Net.Http.HttpClient client, Guid policyId)> CreatePolicyForRuleTestsAsync()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var policyPayload = new
        {
            name = "Rule EP Policy " + Guid.NewGuid().ToString("N")[..6],
            description = (string?)null,
            scopeType = "Student",
            isDefault = false,
            isActive = true,
            rules = Array.Empty<object>()
        };
        var policyResp = await client.PostAsJsonAsync("/api/admin/usage-policies", policyPayload);
        policyResp.EnsureSuccessStatusCode();
        var policyJson = System.Text.Json.JsonDocument.Parse(await policyResp.Content.ReadAsStringAsync());
        var policyId = Guid.Parse(policyJson.RootElement.GetProperty("id").GetString()!);
        return (client, policyId);
    }

    [Fact]
    public async Task Admin_AddRule_ReturnsCreated()
    {
        var (client, policyId) = await CreatePolicyForRuleTestsAsync();

        var rulePayload = new
        {
            featureKey = "writing.evaluate",
            trackingEnabled = true,
            enforcementMode = "HardLimit",
            unitType = "Count",
            dailyLimit = 5,
            weeklyLimit = (int?)null,
            monthlyLimit = (int?)null,
            dailyCostLimit = (decimal?)null,
            monthlyCostLimit = (decimal?)null,
            warningThresholdPercent = 80,
            isActive = true
        };

        var resp = await client.PostAsJsonAsync($"/api/admin/usage-policies/{policyId}/rules", rulePayload);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("writing.evaluate", body);
    }

    [Fact]
    public async Task Admin_AddRule_DuplicateFeatureKey_Returns409()
    {
        var (client, policyId) = await CreatePolicyForRuleTestsAsync();

        var rulePayload = new
        {
            featureKey = "speaking.evaluate",
            trackingEnabled = true,
            enforcementMode = "TrackOnly",
            unitType = "Count",
            dailyLimit = (int?)null,
            weeklyLimit = (int?)null,
            monthlyLimit = (int?)null,
            dailyCostLimit = (decimal?)null,
            monthlyCostLimit = (decimal?)null,
            warningThresholdPercent = 80,
            isActive = true
        };

        var first = await client.PostAsJsonAsync($"/api/admin/usage-policies/{policyId}/rules", rulePayload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/admin/usage-policies/{policyId}/rules", rulePayload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Admin_AddRule_PolicyNotFound_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var rulePayload = new
        {
            featureKey = "writing.evaluate",
            trackingEnabled = true,
            enforcementMode = "TrackOnly",
            unitType = "Count",
            dailyLimit = (int?)null,
            weeklyLimit = (int?)null,
            monthlyLimit = (int?)null,
            dailyCostLimit = (decimal?)null,
            monthlyCostLimit = (decimal?)null,
            warningThresholdPercent = 80,
            isActive = true
        };

        var resp = await client.PostAsJsonAsync($"/api/admin/usage-policies/{Guid.NewGuid()}/rules", rulePayload);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_UpdateRule_ReturnsOk()
    {
        var (client, policyId) = await CreatePolicyForRuleTestsAsync();

        var addPayload = new
        {
            featureKey = "lesson.generate",
            trackingEnabled = true,
            enforcementMode = "TrackOnly",
            unitType = "Count",
            dailyLimit = (int?)null,
            weeklyLimit = (int?)null,
            monthlyLimit = (int?)null,
            dailyCostLimit = (decimal?)null,
            monthlyCostLimit = (decimal?)null,
            warningThresholdPercent = 80,
            isActive = true
        };
        var addResp = await client.PostAsJsonAsync($"/api/admin/usage-policies/{policyId}/rules", addPayload);
        addResp.EnsureSuccessStatusCode();
        var addJson = System.Text.Json.JsonDocument.Parse(await addResp.Content.ReadAsStringAsync());
        var ruleId = Guid.Parse(addJson.RootElement.GetProperty("id").GetString()!);

        var updatePayload = new
        {
            trackingEnabled = false,
            enforcementMode = "HardLimit",
            unitType = "Tokens",
            dailyLimit = 20,
            weeklyLimit = (int?)null,
            monthlyLimit = 100,
            dailyCostLimit = (decimal?)null,
            monthlyCostLimit = (decimal?)null,
            warningThresholdPercent = 70,
            isActive = true
        };
        var updateResp = await client.PutAsJsonAsync(
            $"/api/admin/usage-policies/{policyId}/rules/{ruleId}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var body = await updateResp.Content.ReadAsStringAsync();
        Assert.Contains("HardLimit", body);
        Assert.Contains("Tokens", body);
    }

    [Fact]
    public async Task Admin_UpdateRule_WrongPolicy_Returns404()
    {
        var (client, policyId) = await CreatePolicyForRuleTestsAsync();

        var addPayload = new
        {
            featureKey = "writing.evaluate",
            trackingEnabled = true,
            enforcementMode = "TrackOnly",
            unitType = "Count",
            dailyLimit = (int?)null,
            weeklyLimit = (int?)null,
            monthlyLimit = (int?)null,
            dailyCostLimit = (decimal?)null,
            monthlyCostLimit = (decimal?)null,
            warningThresholdPercent = 80,
            isActive = true
        };
        var addResp = await client.PostAsJsonAsync($"/api/admin/usage-policies/{policyId}/rules", addPayload);
        addResp.EnsureSuccessStatusCode();
        var addJson = System.Text.Json.JsonDocument.Parse(await addResp.Content.ReadAsStringAsync());
        var ruleId = Guid.Parse(addJson.RootElement.GetProperty("id").GetString()!);

        var updatePayload = new
        {
            trackingEnabled = true,
            enforcementMode = "TrackOnly",
            unitType = "Count",
            dailyLimit = (int?)null,
            weeklyLimit = (int?)null,
            monthlyLimit = (int?)null,
            dailyCostLimit = (decimal?)null,
            monthlyCostLimit = (decimal?)null,
            warningThresholdPercent = 80,
            isActive = true
        };
        var resp = await client.PutAsJsonAsync(
            $"/api/admin/usage-policies/{Guid.NewGuid()}/rules/{ruleId}", updatePayload);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_DeleteRule_ReturnsNoContent()
    {
        var (client, policyId) = await CreatePolicyForRuleTestsAsync();

        var addPayload = new
        {
            featureKey = "tts.generate",
            trackingEnabled = true,
            enforcementMode = "TrackOnly",
            unitType = "Count",
            dailyLimit = (int?)null,
            weeklyLimit = (int?)null,
            monthlyLimit = (int?)null,
            dailyCostLimit = (decimal?)null,
            monthlyCostLimit = (decimal?)null,
            warningThresholdPercent = 80,
            isActive = true
        };
        var addResp = await client.PostAsJsonAsync($"/api/admin/usage-policies/{policyId}/rules", addPayload);
        addResp.EnsureSuccessStatusCode();
        var addJson = System.Text.Json.JsonDocument.Parse(await addResp.Content.ReadAsStringAsync());
        var ruleId = Guid.Parse(addJson.RootElement.GetProperty("id").GetString()!);

        var delResp = await client.DeleteAsync($"/api/admin/usage-policies/{policyId}/rules/{ruleId}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);
    }

    [Fact]
    public async Task Admin_DeleteRule_NotFound_Returns404()
    {
        var (client, policyId) = await CreatePolicyForRuleTestsAsync();

        var resp = await client.DeleteAsync(
            $"/api/admin/usage-policies/{policyId}/rules/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_RuleEndpoints_NonAdmin_Returns403()
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync("nonadminrule@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var policyId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        var postResp = await client.PostAsJsonAsync($"/api/admin/usage-policies/{policyId}/rules",
            new { featureKey = "x", trackingEnabled = true, enforcementMode = "TrackOnly", unitType = "Count", warningThresholdPercent = 80, isActive = true });
        Assert.Equal(HttpStatusCode.Forbidden, postResp.StatusCode);

        var putResp = await client.PutAsJsonAsync($"/api/admin/usage-policies/{policyId}/rules/{ruleId}",
            new { trackingEnabled = true, enforcementMode = "TrackOnly", unitType = "Count", warningThresholdPercent = 80, isActive = true });
        Assert.Equal(HttpStatusCode.Forbidden, putResp.StatusCode);

        var delResp = await client.DeleteAsync($"/api/admin/usage-policies/{policyId}/rules/{ruleId}");
        Assert.Equal(HttpStatusCode.Forbidden, delResp.StatusCode);
    }
}
