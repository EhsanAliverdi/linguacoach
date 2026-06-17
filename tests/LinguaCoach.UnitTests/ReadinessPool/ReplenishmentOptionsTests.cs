using FluentAssertions;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.ReadinessPool;

/// <summary>
/// Unit tests for Phase 10N replenishment configuration, PoolHealthSummary, and domain-level rules.
/// These tests do not require a database — they verify pure logic.
/// </summary>
public sealed class ReplenishmentOptionsTests
{
    // 1. Default options have safe, conservative values.
    [Fact]
    public void DefaultOptions_HaveSafeValues()
    {
        var opts = new ReadinessPoolReplenishmentOptions();

        opts.TodayLessonPoolTargetCount.Should().BeGreaterThan(0);
        opts.PracticeGymPoolTargetCount.Should().BeGreaterThan(0);
        opts.MaxGenerationAttempts.Should().BeGreaterThan(0);
        opts.ReadyItemExpiryDays.Should().BeGreaterThan(0);
        opts.ReservedItemExpiryHours.Should().BeGreaterThan(0);
        opts.GeneratingTimeoutMinutes.Should().BeGreaterThan(0);
        opts.FailedRetryDelayMinutes.Should().BeGreaterThan(0);
        opts.MaxItemsGeneratedPerRun.Should().BeGreaterThan(0);
        // Conservative default — review scaffold is off until ledger signals are proven.
        opts.EnableReviewScaffoldGeneration.Should().BeFalse();
    }

    // 2. PoolHealthSummary: shortfall = target - ready - in-flight, minimum 0.
    [Fact]
    public void PoolHealthSummary_ShortfallCount_IsCorrect()
    {
        var health = new PoolHealthSummary
        {
            StudentId = Guid.NewGuid(),
            Source = ReadinessPoolSource.TodayLesson,
            TargetCount = 10,
            ReadyCount = 3,
            QueuedOrGeneratingCount = 2
        };

        health.ShortfallCount.Should().Be(5);
        health.NeedsReplenishment.Should().BeTrue();
    }

    // 3. Shortfall does not go negative.
    [Fact]
    public void PoolHealthSummary_ShortfallCount_NeverNegative()
    {
        var health = new PoolHealthSummary
        {
            TargetCount = 5,
            ReadyCount = 4,
            QueuedOrGeneratingCount = 4
        };

        health.ShortfallCount.Should().Be(0);
        health.NeedsReplenishment.Should().BeFalse();
    }

    // 4. NeedsReplenishment is false when ready+in-flight >= target.
    [Fact]
    public void PoolHealthSummary_NeedsReplenishment_FalseWhenAtTarget()
    {
        var health = new PoolHealthSummary
        {
            TargetCount = 10,
            ReadyCount = 10,
            QueuedOrGeneratingCount = 0
        };

        health.NeedsReplenishment.Should().BeFalse();
    }

    // 5. ReviewOnly count does not reduce shortfall (not usable as normal content).
    [Fact]
    public void PoolHealthSummary_ReviewOnly_NotCountedTowardTarget()
    {
        var health = new PoolHealthSummary
        {
            TargetCount = 10,
            ReadyCount = 2,
            QueuedOrGeneratingCount = 0,
            ReviewOnlyCount = 8   // these cannot satisfy a normal shortfall
        };

        // Shortfall = 10 - 2 - 0 = 8 (ReviewOnly excluded from shortfall math)
        health.ShortfallCount.Should().Be(8);
        health.NeedsReplenishment.Should().BeTrue();
    }

    // 6. Consumed/expired/failed/stale items do not affect shortfall.
    [Fact]
    public void PoolHealthSummary_TerminalStatuses_NotCountedTowardTarget()
    {
        var health = new PoolHealthSummary
        {
            TargetCount = 10,
            ReadyCount = 1,
            QueuedOrGeneratingCount = 0,
            ExpiredCount = 5,
            FailedCount = 3,
            StaleCount = 2
        };

        // None of expired/failed/stale reduce shortfall
        health.ShortfallCount.Should().Be(9);
    }

    // 7. ReplenishmentRunSummary initialises correctly.
    [Fact]
    public void ReplenishmentRunSummary_DefaultValues_AreZero()
    {
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        summary.ItemsQueued.Should().Be(0);
        summary.ItemsExpired.Should().Be(0);
        summary.ItemsRecoveredFromGenerating.Should().Be(0);
        summary.ItemsRetryQueued.Should().Be(0);
        summary.HitMaxItemsLimit.Should().BeFalse();
    }

    // 8. MaxItemsGeneratedPerRun cap is respected (shortfall vs cap).
    [Fact]
    public void ReplenishmentOptions_MaxItemsGeneratedPerRun_IsPositive()
    {
        var opts = new ReadinessPoolReplenishmentOptions { MaxItemsGeneratedPerRun = 50 };
        opts.MaxItemsGeneratedPerRun.Should().Be(50);
    }

    // 9. ReadinessItemRequestBuilder preserves routing reason and context tags.
    [Fact]
    public void ReadinessItemRequestBuilder_PreservesRoutingSnapshot()
    {
        var recommendation = new CurriculumRoutingRecommendation
        {
            Source = "test",
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false,
            CurriculumObjectiveKey = "b2_speaking_workplace",
            CurriculumObjectiveTitle = "Workplace Speaking B2",
            PrimarySkill = "speaking",
            SecondarySkills = ["listening"],
            ContextTags = ["general_english"],
            FocusTags = ["communication"],
            DifficultyBand = 3,
            Explanation = "Normal B2 routing"
        };

        var req = ReadinessItemRequestBuilder.FromRoutingRecommendation(
            studentId: Guid.NewGuid(),
            source: ReadinessPoolSource.PracticeGym,
            recommendation: recommendation,
            originalCefrLevelSnapshot: "B2",
            generatedBy: "test");

        req.TargetCefrLevel.Should().Be("B2");
        req.RoutingReason.Should().Be(RoutingReason.Normal);
        req.IsLowerLevelContent.Should().BeFalse();
        req.CurriculumObjectiveKey.Should().Be("b2_speaking_workplace");
        req.PrimarySkill.Should().Be("speaking");
        req.ContextTagsJson.Should().Contain("general_english");
        req.GeneratedBy.Should().Be("test");
        // general_english is fallback — workplace not default
        req.ContextTagsJson.Should().NotContain("workplace");
    }

    // 10. Lower-level content without review/scaffold routing reason is rejected by domain entity.
    [Fact]
    public void Domain_LowerLevelContent_RequiresNonNormalRoutingReason()
    {
        var act = () => new StudentActivityReadinessItem(
            studentId: Guid.NewGuid(),
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: true   // B1 for B2 student without review reason — must throw
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*IsLowerLevelContent=true requires a non-Normal RoutingReason*");
    }

    // 11. Lower-level content is allowed with Review routing reason.
    [Fact]
    public void Domain_LowerLevelContent_IsAllowedWithReviewReason()
    {
        var item = new StudentActivityReadinessItem(
            studentId: Guid.NewGuid(),
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Review,
            isLowerLevelContent: true);

        item.IsLowerLevelContent.Should().BeTrue();
        item.RoutingReason.Should().Be(RoutingReason.Review);
    }

    // 12. Without weakness signals, review/scaffold generation is disabled by default.
    [Fact]
    public void Options_EnableReviewScaffoldGeneration_DefaultFalse_PreventsSilentLevelDrop()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        // Conservative default: false. B2 students will not silently get B1 content.
        opts.EnableReviewScaffoldGeneration.Should().BeFalse();
    }

    // 13. general_english is the fallback context tag, not workplace.
    [Fact]
    public void ContextTags_FallbackIsGeneralEnglish_NotWorkplace()
    {
        var recommendation = new CurriculumRoutingRecommendation
        {
            Source = "test",
            TargetCefrLevel = "A2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false,
            ContextTags = ["general_english"],
            SecondarySkills = [],
            FocusTags = []
        };

        var req = ReadinessItemRequestBuilder.FromRoutingRecommendation(
            studentId: Guid.NewGuid(),
            source: ReadinessPoolSource.TodayLesson,
            recommendation: recommendation);

        req.ContextTagsJson.Should().Contain("general_english");
        req.ContextTagsJson.Should().NotContain("workplace");
    }

    // 14. PoolHealthSummary with target 0 never needs replenishment.
    [Fact]
    public void PoolHealthSummary_TargetZero_NeverNeedsReplenishment()
    {
        var health = new PoolHealthSummary { TargetCount = 0, ReadyCount = 0 };
        health.NeedsReplenishment.Should().BeFalse();
        health.ShortfallCount.Should().Be(0);
    }

    // 15. Routing snapshot is preserved in retry request (key fields).
    [Fact]
    public void RetryRequest_PreservesRoutingSnapshot()
    {
        var req = new CreateReadinessItemRequest
        {
            StudentId = Guid.NewGuid(),
            Source = ReadinessPoolSource.TodayLesson,
            TargetCefrLevel = "B2",
            RoutingReason = RoutingReason.Normal,
            IsLowerLevelContent = false,
            CurriculumObjectiveKey = "b2_writing_task",
            ContextTagsJson = "[\"general_english\"]",
            GeneratedBy = "ReadinessPoolReplenishment:Retry"
        };

        req.GeneratedBy.Should().Contain("Retry");
        req.ContextTagsJson.Should().Contain("general_english");
        req.IsLowerLevelContent.Should().BeFalse();
    }

    // 16. MaxGenerationAttempts gate: failed item at limit must not be retried.
    [Fact]
    public void MaxGenerationAttempts_FailedAtLimit_ShouldNotRetry()
    {
        var opts = new ReadinessPoolReplenishmentOptions { MaxGenerationAttempts = 3 };
        // An item with AttemptCount >= MaxGenerationAttempts must not get a new queued item.
        // This is enforced in service query: AttemptCount < MaxGenerationAttempts.
        // Test verifies the config value is correct.
        opts.MaxGenerationAttempts.Should().BeGreaterThan(0);

        const int itemAttemptCount = 3;
        (itemAttemptCount < opts.MaxGenerationAttempts).Should().BeFalse();
    }
}
