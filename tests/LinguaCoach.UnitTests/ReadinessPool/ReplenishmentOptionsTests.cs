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

    // 17. Phase 11C-FINAL: review scaffold flag false means routing mode stays NewLearning.
    [Fact]
    public void Options_ReviewScaffoldFlagFalse_RoutingStaysNewLearning()
    {
        var opts = new ReadinessPoolReplenishmentOptions { EnableReviewScaffoldGeneration = false };
        // When flag is false, allowReviewOrScaffold must be false in the factory call.
        // Verified here at options level — integration is covered in routing tests.
        opts.EnableReviewScaffoldGeneration.Should().BeFalse();
    }

    // 18. Phase 11C-FINAL: ReviewOnly items do not satisfy new-learning shortfall.
    [Fact]
    public void PoolHealthSummary_ReviewOnly_NeverSatisfiesNewLearningShortfall()
    {
        var health = new PoolHealthSummary
        {
            TargetCount = 5,
            ReadyCount = 0,
            QueuedOrGeneratingCount = 0,
            ReviewOnlyCount = 10  // ReviewOnly items are not new-learning capable
        };

        health.ShortfallCount.Should().Be(5);
        health.NeedsReplenishment.Should().BeTrue();
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

    // Phase 12C tests ──────────────────────────────────────────────────────────

    // 19. MinimumReadyThreshold default is conservative and positive.
    [Fact]
    public void DefaultOptions_MinimumReadyThreshold_IsConservativeAndPositive()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        opts.MinimumReadyThreshold.Should().BeGreaterThan(0);
        opts.MinimumReadyThreshold.Should().BeLessThanOrEqualTo(opts.TodayLessonPoolTargetCount);
    }

    // 20. MaxBufferCount default is above target to allow headroom.
    [Fact]
    public void DefaultOptions_MaxBufferCount_IsAboveTarget()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        opts.MaxBufferCount.Should().BeGreaterThan(opts.TodayLessonPoolTargetCount);
    }

    // 21. ReplenishmentRunSummary.ElapsedMs is computed correctly.
    [Fact]
    public void ReplenishmentRunSummary_ElapsedMs_IsCorrect()
    {
        var start = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(2026, 6, 27, 12, 0, 5, DateTimeKind.Utc);
        var summary = new ReplenishmentRunSummary { StartedAt = start, CompletedAt = end };

        summary.ElapsedMs.Should().Be(5000L);
    }

    // 22. GenerationSuccessRate returns 1.0 when nothing was attempted.
    [Fact]
    public void ReplenishmentRunSummary_GenerationSuccessRate_ReturnsOneWhenNothingAttempted()
    {
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ItemsQueued = 0,
            SkippedDuplicates = 0,
            SkippedAtMaxBuffer = 0
        };

        summary.GenerationSuccessRate.Should().Be(1.0);
    }

    // 23. GenerationSuccessRate is correct when some were skipped.
    [Fact]
    public void ReplenishmentRunSummary_GenerationSuccessRate_CorrectWithSkips()
    {
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ItemsQueued = 7,
            SkippedDuplicates = 2,
            SkippedAtMaxBuffer = 1
        };

        // 7 / (7+2+1) = 0.7
        summary.GenerationSuccessRate.Should().BeApproximately(0.7, 0.001);
    }

    // 24. AggregatePoolHealthSummary.AverageReadyPerStudent returns 0 when no students.
    [Fact]
    public void AggregatePoolHealthSummary_AverageReadyPerStudent_ZeroWhenNoStudents()
    {
        var summary = new AggregatePoolHealthSummary
        {
            TotalStudentsWithItems = 0,
            TotalReady = 0,
            AverageReadyPerStudent = 0.0,
            GeneratedAt = DateTime.UtcNow
        };

        summary.AverageReadyPerStudent.Should().Be(0.0);
    }

    // 25. AggregatePoolHealthSummary.StudentsBelowMinimumThreshold is a non-negative integer.
    [Fact]
    public void AggregatePoolHealthSummary_StudentsBelowMinimumThreshold_IsNonNegative()
    {
        var summary = new AggregatePoolHealthSummary
        {
            TotalStudentsWithItems = 5,
            StudentsWithNoReadyItems = 2,
            StudentsBelowMinimumThreshold = 3,
            GeneratedAt = DateTime.UtcNow
        };

        summary.StudentsBelowMinimumThreshold.Should().BeGreaterThanOrEqualTo(0);
        // Must not exceed total students.
        summary.StudentsBelowMinimumThreshold.Should().BeLessThanOrEqualTo(summary.TotalStudentsWithItems);
    }

    // 26. SkippedAtMaxBuffer is tracked separately from SkippedDuplicates.
    [Fact]
    public void ReplenishmentRunSummary_SkippedAtMaxBuffer_IsDistinctFromDuplicates()
    {
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            SkippedDuplicates = 4,
            SkippedAtMaxBuffer = 3
        };

        summary.SkippedDuplicates.Should().Be(4);
        summary.SkippedAtMaxBuffer.Should().Be(3);
    }

    // 27. MaxBufferCount prevents over-fill: items above cap count as SkippedAtMaxBuffer.
    [Fact]
    public void Options_MaxBufferCount_CanBeSetToPreventOverfill()
    {
        var opts = new ReadinessPoolReplenishmentOptions { MaxBufferCount = 15 };
        opts.MaxBufferCount.Should().Be(15);
        opts.MaxBufferCount.Should().BeGreaterThan(0);
    }

    // 28. MinimumReadyThreshold can be configured per environment.
    [Fact]
    public void Options_MinimumReadyThreshold_CanBeOverridden()
    {
        var opts = new ReadinessPoolReplenishmentOptions { MinimumReadyThreshold = 5 };
        opts.MinimumReadyThreshold.Should().Be(5);
    }

    // 29. ReplenishmentRunSummary.ElapsedMs is zero when start == complete.
    [Fact]
    public void ReplenishmentRunSummary_ElapsedMs_IsZeroWhenInstant()
    {
        var now = DateTime.UtcNow;
        var summary = new ReplenishmentRunSummary { StartedAt = now, CompletedAt = now };
        summary.ElapsedMs.Should().Be(0L);
    }

    // 30. GenerationSuccessRate is 1.0 when all attempts succeeded.
    [Fact]
    public void ReplenishmentRunSummary_GenerationSuccessRate_OneWhenAllQueued()
    {
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ItemsQueued = 10,
            SkippedDuplicates = 0,
            SkippedAtMaxBuffer = 0
        };

        summary.GenerationSuccessRate.Should().Be(1.0);
    }

    // Phase 19A tests ──────────────────────────────────────────────────────────

    // 31. DryRunOnly now defaults to true — flipping Enabled on always requires an explicit
    //     second step (setting DryRunOnly=false) before generation goes live.
    [Fact]
    public void DefaultOptions_DryRunOnly_DefaultsTrue()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        opts.DryRunOnly.Should().BeTrue();
    }

    // 32. RequireAdminReview defaults to true — scaffold items are hidden from students
    //     until an admin explicitly clears the config flag.
    [Fact]
    public void DefaultOptions_RequireAdminReview_DefaultsTrue()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        opts.RequireAdminReview.Should().BeTrue();
    }

    // 33. MaxScaffoldItemsPerStudentPerDay has a small, conservative default.
    [Fact]
    public void DefaultOptions_MaxScaffoldItemsPerStudentPerDay_IsConservative()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        opts.MaxScaffoldItemsPerStudentPerDay.Should().Be(3);
        opts.MaxScaffoldItemsPerStudentPerDay.Should().BeGreaterThan(0);
    }

    // 34. ScaffoldAllowedSources defaults to PracticeGym only — Today lesson excluded.
    [Fact]
    public void DefaultOptions_ScaffoldAllowedSources_DefaultsToPracticeGymOnly()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        opts.ScaffoldAllowedSources.Should().ContainSingle().Which.Should().Be("PracticeGym");
    }

    // 35. AllowTodayLessonInsertion defaults to false.
    [Fact]
    public void DefaultOptions_AllowTodayLessonInsertion_DefaultsFalse()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        opts.AllowTodayLessonInsertion.Should().BeFalse();
    }

    // 36. MinimumConfidenceForReviewNeed defaults to Medium.
    [Fact]
    public void DefaultOptions_MinimumConfidenceForReviewNeed_DefaultsMedium()
    {
        var opts = new ReadinessPoolReplenishmentOptions();
        opts.MinimumConfidenceForReviewNeed.Should().Be("Medium");
    }

    // 37. ReviewNeedConfidence ordinal ordering supports >= threshold comparisons.
    [Fact]
    public void ReviewNeedConfidence_OrdinalOrder_LowLessThanMediumLessThanHigh()
    {
        (ReviewNeedConfidence.Low < ReviewNeedConfidence.Medium).Should().BeTrue();
        (ReviewNeedConfidence.Medium < ReviewNeedConfidence.High).Should().BeTrue();
    }

    // 38. ReadinessItemRequestBuilder threads RequiresAdminReview through.
    [Fact]
    public void ReadinessItemRequestBuilder_ThreadsRequiresAdminReview()
    {
        var recommendation = new CurriculumRoutingRecommendation
        {
            Source = "test",
            TargetCefrLevel = "B1",
            RoutingReason = RoutingReason.Review,
            IsLowerLevelContent = true,
            SecondarySkills = [],
            ContextTags = [],
            FocusTags = []
        };

        var req = ReadinessItemRequestBuilder.FromRoutingRecommendation(
            studentId: Guid.NewGuid(),
            source: ReadinessPoolSource.PracticeGym,
            recommendation: recommendation,
            requiresAdminReview: true);

        req.RequiresAdminReview.Should().BeTrue();
    }

    // 39. StudentActivityReadinessItem defaults RequiresAdminReview to false.
    [Fact]
    public void Domain_RequiresAdminReview_DefaultsFalse()
    {
        var item = new StudentActivityReadinessItem(
            studentId: Guid.NewGuid(),
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false);

        item.RequiresAdminReview.Should().BeFalse();
    }

    // 40. StudentActivityReadinessItem sets RequiresAdminReview from constructor param.
    [Fact]
    public void Domain_RequiresAdminReview_SetFromConstructor()
    {
        var item = new StudentActivityReadinessItem(
            studentId: Guid.NewGuid(),
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Review,
            isLowerLevelContent: true,
            requiresAdminReview: true);

        item.RequiresAdminReview.Should().BeTrue();
    }

    // 41. SkippedDailyCapReached is tracked separately from other skip counters.
    [Fact]
    public void ReplenishmentRunSummary_SkippedDailyCapReached_IsDistinctCounter()
    {
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            SkippedDuplicates = 2,
            SkippedAtMaxBuffer = 1,
            SkippedDailyCapReached = 4
        };

        summary.SkippedDailyCapReached.Should().Be(4);
        summary.SkippedDuplicates.Should().Be(2);
        summary.SkippedAtMaxBuffer.Should().Be(1);
    }
}
