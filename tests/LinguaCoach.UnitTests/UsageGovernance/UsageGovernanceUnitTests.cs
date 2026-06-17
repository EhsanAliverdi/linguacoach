using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Xunit;

namespace LinguaCoach.UnitTests.UsageGovernance;

/// <summary>
/// Phase 10R — Usage governance unit tests (20 tests).
/// </summary>
public sealed class UsageGovernanceUnitTests
{
    // ── 1. FeatureDefinition validation ──────────────────────────────────────

    [Fact]
    public void FeatureDefinition_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new FeatureDefinition("", "Name", null, FeatureCategory.PreparedLearning,
                EnforcementMode.TrackOnly, UsageUnitType.Count, false, true, true));
    }

    [Fact]
    public void FeatureDefinition_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new FeatureDefinition("key", "", null, FeatureCategory.PreparedLearning,
                EnforcementMode.TrackOnly, UsageUnitType.Count, false, true, true));
    }

    [Fact]
    public void FeatureDefinition_ValidConstruction_SetsKeyLowercase()
    {
        var def = new FeatureDefinition("Writing.Evaluate", "Evaluate Writing", null,
            FeatureCategory.ExpensiveAi, EnforcementMode.TrackOnly, UsageUnitType.Count, true, true, true);
        Assert.Equal("writing.evaluate", def.Key);
    }

    // ── 2. UsagePolicy validation ─────────────────────────────────────────────

    [Fact]
    public void UsagePolicy_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new UsagePolicy("", null, UsagePolicyScopeType.Global, true, true));
    }

    [Fact]
    public void UsagePolicy_ValidConstruction_Succeeds()
    {
        var policy = new UsagePolicy("Default", "desc", UsagePolicyScopeType.Global, true, true);
        Assert.Equal("Default", policy.Name);
        Assert.True(policy.IsDefault);
        Assert.True(policy.IsActive);
    }

    // ── 3. UsagePolicyRule validation ─────────────────────────────────────────

    [Fact]
    public void UsagePolicyRule_NegativeDailyLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UsagePolicyRule(Guid.NewGuid(), "writing.evaluate", true,
                EnforcementMode.HardLimit, UsageUnitType.Count,
                dailyLimit: -1, null, null, null, null, 80, true));
    }

    [Fact]
    public void UsagePolicyRule_NegativeMonthlyCostLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UsagePolicyRule(Guid.NewGuid(), "writing.evaluate", true,
                EnforcementMode.HardLimit, UsageUnitType.Cost,
                null, null, null, null, monthlyCostLimit: -1m, 80, true));
    }

    [Fact]
    public void UsagePolicyRule_WarningThresholdOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UsagePolicyRule(Guid.NewGuid(), "writing.evaluate", true,
                EnforcementMode.TrackOnly, UsageUnitType.Count,
                null, null, null, null, null, warningThresholdPercent: 150, true));
    }

    [Fact]
    public void UsagePolicyRule_EmptyFeatureKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new UsagePolicyRule(Guid.NewGuid(), "", true,
                EnforcementMode.TrackOnly, UsageUnitType.Count,
                null, null, null, null, null, 80, true));
    }

    [Fact]
    public void UsagePolicyRule_ValidLimits_Succeeds()
    {
        var rule = new UsagePolicyRule(Guid.NewGuid(), "writing.evaluate", true,
            EnforcementMode.HardLimit, UsageUnitType.Count,
            dailyLimit: 5, weeklyLimit: 20, monthlyLimit: 50,
            dailyCostLimit: null, monthlyCostLimit: null, 80, true);

        Assert.Equal(5, rule.DailyLimit);
        Assert.Equal(50, rule.MonthlyLimit);
        Assert.Equal(EnforcementMode.HardLimit, rule.EnforcementMode);
    }

    // ── 4. QuotaDecision factory methods ─────────────────────────────────────

    [Fact]
    public void QuotaDecision_Allow_IsAllowed()
    {
        var d = QuotaDecision.Allow("writing.evaluate");
        Assert.True(d.Allowed);
        Assert.Equal("writing.evaluate", d.FeatureKey);
    }

    [Fact]
    public void QuotaDecision_Block_HasAlternatives()
    {
        var alternatives = new[] { "lesson.view", "practice.prepared.complete" };
        var d = QuotaDecision.Block("writing.evaluate", "Daily limit reached.", EnforcementMode.HardLimit, alternatives);
        Assert.False(d.Allowed);
        Assert.Equal(2, d.AvailableAlternatives.Count);
        Assert.Contains("lesson.view", d.AvailableAlternatives);
    }

    [Fact]
    public void QuotaDecision_Block_SetsReason()
    {
        var d = QuotaDecision.Block("speaking.evaluate", "Monthly limit reached.", EnforcementMode.HardLimit);
        Assert.Equal("Monthly limit reached.", d.Reason);
        Assert.False(d.Allowed);
    }

    // ── 5. UsageEvent validation ──────────────────────────────────────────────

    [Fact]
    public void UsageEvent_NegativeUnits_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UsageEvent(Guid.NewGuid(), "writing.evaluate", UsageUnitType.Count,
                unitsUsed: -1, "openai", "gpt-4", 100, 200, 300, 0.01m, null, null, true));
    }

    [Fact]
    public void UsageEvent_RecordsProviderAndModel()
    {
        var ev = new UsageEvent(Guid.NewGuid(), "writing.evaluate", UsageUnitType.Count,
            1, "anthropic", "claude-sonnet-4-6", 500, 300, 800, 0.05m, "req-123", "corr-456", true);

        Assert.Equal("anthropic", ev.Provider);
        Assert.Equal("claude-sonnet-4-6", ev.Model);
        Assert.Equal(500, ev.InputTokens);
        Assert.Equal(300, ev.OutputTokens);
        Assert.Equal(800, ev.TotalTokens);
        Assert.Equal("req-123", ev.RequestId);
        Assert.Equal("corr-456", ev.CorrelationId);
    }

    [Fact]
    public void UsageEvent_SuccessFalse_RecordsError()
    {
        var ev = new UsageEvent(Guid.NewGuid(), "writing.evaluate", UsageUnitType.Count,
            0, null, null, 0, 0, 0, null, null, null,
            success: false, errorCode: "TIMEOUT", errorMessage: "Provider timed out.");

        Assert.False(ev.Success);
        Assert.Equal("TIMEOUT", ev.ErrorCode);
    }

    // ── 6. StudentUsageDaily.Apply aggregation ────────────────────────────────

    [Fact]
    public void StudentUsageDaily_Apply_AccumulatesTokensAndCost()
    {
        var agg = new StudentUsageDaily(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        agg.Apply(100, 200, 300, 0.03m, true, 0m, 0, 0m, "writing.evaluate");
        agg.Apply(50,  100, 150, 0.02m, true, 0m, 0, 0m, "writing.evaluate");

        Assert.Equal(150, agg.InputTokens);
        Assert.Equal(300, agg.OutputTokens);
        Assert.Equal(450, agg.TotalTokens);
        Assert.Equal(0.05m, agg.TotalCost);
        Assert.Equal(2, agg.AiCallCount);
        Assert.Equal(2, agg.WritingEvaluations);
    }

    [Fact]
    public void StudentUsageDaily_Apply_PreparedActivity_DoesNotIncrementAiCallCount()
    {
        var agg = new StudentUsageDaily(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        agg.Apply(0, 0, 0, 0m, false, 0m, 0, 0m, "practice.prepared.complete");

        Assert.Equal(0, agg.AiCallCount);
        Assert.Equal(1, agg.PreparedActivitiesCompleted);
    }

    // ── 7. QuotaExceededException ─────────────────────────────────────────────

    [Fact]
    public void QuotaExceededException_MessageFromDecision()
    {
        var decision = QuotaDecision.Block("tts.generate", "Daily TTS limit reached.", EnforcementMode.HardLimit);
        var ex = new QuotaExceededException(decision);
        Assert.Equal("Daily TTS limit reached.", ex.Message);
        Assert.Same(decision, ex.Decision);
    }
}
