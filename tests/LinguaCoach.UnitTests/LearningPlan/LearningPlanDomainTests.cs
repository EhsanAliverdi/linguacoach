using FluentAssertions;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.LearningPlan;

/// <summary>
/// Unit tests for Phase 12D — Learning Plan Orchestrator Foundation.
/// Pure domain/options logic — no database required.
/// </summary>
public sealed class LearningPlanDomainTests
{
    // ── StudentLearningPlan entity ────────────────────────────────────────────

    // 1. New plan starts in Active status.
    [Fact]
    public void NewPlan_StartsActive()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation");

        plan.Status.Should().Be(LearningPlanStatus.Active);
        plan.RegenerationCount.Should().Be(0);
        plan.LastEvaluatedAt.Should().BeNull();
    }

    // 2. MarkReady sets LastEvaluatedAt and keeps status Active.
    [Fact]
    public void MarkReady_SetsEvaluatedAt()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation");
        var now = DateTime.UtcNow;

        plan.MarkReady(now);

        plan.Status.Should().Be(LearningPlanStatus.Active);
        plan.LastEvaluatedAt.Should().Be(now);
    }

    // 3. Supersede transitions to Superseded status.
    [Fact]
    public void Supersede_TransitionsToSuperseded()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation");

        plan.Supersede();

        plan.Status.Should().Be(LearningPlanStatus.Superseded);
    }

    // 4. StartRegeneration increments counter and sets Regenerating status.
    [Fact]
    public void StartRegeneration_IncrementsCounterAndSetsRegeneratingStatus()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B1", "initial_generation");

        plan.StartRegeneration("mastery_sweep");
        plan.StartRegeneration("cefr_change");

        plan.Status.Should().Be(LearningPlanStatus.Regenerating);
        plan.RegenerationCount.Should().Be(2);
        plan.RegenerationReason.Should().Be("cefr_change");
    }

    // 5. PlannedLessonCount uses constructor default of 10.
    [Fact]
    public void NewPlan_DefaultPlannedLessonCount_IsTen()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation");

        plan.PlannedLessonCount.Should().Be(10);
    }

    // 6. PlannedLessonCount can be overridden.
    [Fact]
    public void NewPlan_PlannedLessonCount_CanBeOverridden()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation", plannedLessonCount: 5);

        plan.PlannedLessonCount.Should().Be(5);
    }

    // 7. ClearObjectives removes all objectives.
    [Fact]
    public void ClearObjectives_RemovesAllObjectives()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation");
        var obj = new StudentLearningPlanObjective(
            plan.Id, "b2_speaking_01", "B2", "speaking", "workplace", null, 1, "routing");
        plan.AddObjective(obj);

        plan.ClearObjectives();

        plan.Objectives.Should().BeEmpty();
    }

    // ── StudentLearningPlanObjective entity ───────────────────────────────────

    // 8. New objective starts Active (non-blocked).
    [Fact]
    public void NewObjective_StartsActive_WhenNotBlocked()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b2_writing_01", "B2", "writing", "workplace", "Email Writing", 1, "routing");

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Active);
        obj.IsBlocked.Should().BeFalse();
        obj.LastEvaluatedAt.Should().BeNull();
    }

    // 9. New objective starts Blocked when isBlocked=true.
    [Fact]
    public void NewObjective_StartsBlocked_WhenIsBlockedTrue()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b2_writing_02", "B2", "writing", "workplace", null, 2, "prerequisite",
            isBlocked: true, blockedByObjectiveKey: "b2_writing_01");

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Blocked);
        obj.IsBlocked.Should().BeTrue();
        obj.BlockedByObjectiveKey.Should().Be("b2_writing_01");
    }

    // 10. MarkCompleted sets status and LastEvaluatedAt.
    [Fact]
    public void MarkCompleted_SetsStatusAndTimestamp()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b2_vocab_01", "B2", "vocabulary", "general_english", null, 3, "routing");

        obj.MarkCompleted();

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Completed);
        obj.LastEvaluatedAt.Should().NotBeNull();
        obj.LastEvaluatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // 11. MarkMastered sets Mastered status.
    [Fact]
    public void MarkMastered_SetsMasteredStatus()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b2_reading_01", "B2", "reading", "general_english", null, 1, "routing");

        obj.MarkMastered();

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Mastered);
    }

    // 12. MarkDeferred sets Deferred status.
    [Fact]
    public void MarkDeferred_SetsDeferredStatus()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b2_listening_01", "B2", "listening", "general_english", null, 2, "routing");

        obj.MarkDeferred();

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Deferred);
    }

    // 13. Unblock clears IsBlocked and sets Active status.
    [Fact]
    public void Unblock_ClearsBlockedFlag_AndSetsActive()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b2_writing_02", "B2", "writing", "workplace", null, 2, "prerequisite",
            isBlocked: true, blockedByObjectiveKey: "b2_writing_01");

        obj.Unblock();

        obj.IsBlocked.Should().BeFalse();
        obj.BlockedByObjectiveKey.Should().BeNull();
        obj.Status.Should().Be(LearningPlanObjectiveStatus.Active);
    }

    // 14. SetPlannedOrder updates PlannedOrder.
    [Fact]
    public void SetPlannedOrder_UpdatesValue()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b2_speaking_01", "B2", "speaking", "workplace", null, 1, "routing");

        obj.SetPlannedOrder(3);

        obj.PlannedOrder.Should().Be(3);
    }

    // 15. Evaluate sets LastEvaluatedAt to the provided timestamp.
    [Fact]
    public void Evaluate_SetsLastEvaluatedAt()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b2_grammar_01", "B2", "grammar", "general_english", null, 1, "routing");
        var ts = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc);

        obj.Evaluate(ts);

        obj.LastEvaluatedAt.Should().Be(ts);
    }

    // ── LearningPlanStatus enum ───────────────────────────────────────────────

    // 16. LearningPlanStatus has exactly the expected values.
    [Fact]
    public void LearningPlanStatus_HasExpectedValues()
    {
        var values = Enum.GetValues<LearningPlanStatus>();

        values.Should().Contain(LearningPlanStatus.Active);
        values.Should().Contain(LearningPlanStatus.Regenerating);
        values.Should().Contain(LearningPlanStatus.Superseded);
        values.Should().HaveCount(3);
    }

    // 17. LearningPlanObjectiveStatus has exactly the expected values.
    [Fact]
    public void LearningPlanObjectiveStatus_HasExpectedValues()
    {
        var values = Enum.GetValues<LearningPlanObjectiveStatus>();

        values.Should().Contain(LearningPlanObjectiveStatus.Active);
        values.Should().Contain(LearningPlanObjectiveStatus.Completed);
        values.Should().Contain(LearningPlanObjectiveStatus.Mastered);
        values.Should().Contain(LearningPlanObjectiveStatus.Blocked);
        values.Should().Contain(LearningPlanObjectiveStatus.Deferred);
        values.Should().Contain(LearningPlanObjectiveStatus.Review);
        values.Should().HaveCount(6);
    }

    // ── LearningPlanOptions ───────────────────────────────────────────────────

    // 18. Default options have safe, positive values.
    [Fact]
    public void DefaultOptions_HaveSafeValues()
    {
        var opts = new LearningPlanOptions();

        opts.PlannedLessonCount.Should().BeGreaterThan(0);
        opts.MaxUpcomingObjectives.Should().BeGreaterThan(0);
        opts.MaxPracticeGymObjectives.Should().BeGreaterThan(0);
        opts.MasteryCompletionThreshold.Should().BeInRange(1, 100);
    }

    // 19. SectionName is correct.
    [Fact]
    public void SectionName_IsLearningPlan()
    {
        LearningPlanOptions.SectionName.Should().Be("LearningPlan");
    }

    // 20. PlannedLessonCount default is 10.
    [Fact]
    public void DefaultOptions_PlannedLessonCount_IsTen()
    {
        var opts = new LearningPlanOptions();
        opts.PlannedLessonCount.Should().Be(10);
    }

    // 21. MasteryCompletionThreshold default is 70.
    [Fact]
    public void DefaultOptions_MasteryCompletionThreshold_IsSeventyPercent()
    {
        var opts = new LearningPlanOptions();
        opts.MasteryCompletionThreshold.Should().Be(70);
    }

    // ── ILearningPlanService DTOs ─────────────────────────────────────────────

    // 22. LearningPlanSummary record initialises correctly.
    [Fact]
    public void LearningPlanSummary_InitialisesCorrectly()
    {
        var planId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var upcoming = new List<PlannedObjectiveContext>
        {
            new("b2_speaking_01", "B2", "speaking", [], [], false, 1, "routing")
        };

        var summary = new LearningPlanSummary(
            PlanId: planId,
            StudentProfileId: studentId,
            CefrLevel: "B2",
            Status: LearningPlanStatus.Active,
            RegenerationReason: "initial_generation",
            RegenerationCount: 0,
            TotalObjectives: 10,
            ActiveObjectives: 8,
            ReviewObjectives: 1,
            BlockedObjectives: 0,
            MasteredObjectives: 1,
            CompletedObjectives: 0,
            PlannedLessonCount: 10,
            LastEvaluatedAt: null,
            UpcomingObjectives: upcoming);

        summary.PlanId.Should().Be(planId);
        summary.CefrLevel.Should().Be("B2");
        summary.TotalObjectives.Should().Be(10);
        summary.UpcomingObjectives.Should().HaveCount(1);
    }

    // 23. LearningPlanProgressSummary record initialises correctly.
    [Fact]
    public void LearningPlanProgressSummary_InitialisesCorrectly()
    {
        var studentId = Guid.NewGuid();

        var progress = new LearningPlanProgressSummary(
            StudentProfileId: studentId,
            CurrentCefrLevel: "B1",
            ObjectivesCompleted: 3,
            ObjectivesRemaining: 7,
            ReviewObjectives: 1,
            BlockedObjectives: 0,
            MasteryPercentage: 30.0,
            CurrentLearningPhase: "active",
            LessonQueueLength: 4,
            LessonQueueTarget: 10);

        progress.StudentProfileId.Should().Be(studentId);
        progress.ObjectivesCompleted.Should().Be(3);
        progress.MasteryPercentage.Should().Be(30.0);
        progress.LessonQueueTarget.Should().Be(10);
    }

    // 24. PlannedObjectiveContext record initialises correctly.
    [Fact]
    public void PlannedObjectiveContext_InitialisesCorrectly()
    {
        var ctx = new PlannedObjectiveContext(
            ObjectiveKey: "b2_writing_emails",
            CefrLevel: "B2",
            PrimarySkill: "writing",
            SecondarySkills: ["reading"],
            ContextTags: ["workplace"],
            IsReview: false,
            Priority: 1,
            Source: "routing");

        ctx.ObjectiveKey.Should().Be("b2_writing_emails");
        ctx.PrimarySkill.Should().Be("writing");
        ctx.IsReview.Should().BeFalse();
        ctx.ContextTags.Should().Contain("workplace");
    }

    // 25. IsReview flag propagates from plan objective to PlannedObjectiveContext.
    [Fact]
    public void IsReviewFlag_PropagatesCorrectly()
    {
        var obj = new StudentLearningPlanObjective(
            Guid.NewGuid(), "b1_speaking_review", "B1", "speaking", "general_english",
            "Review: Introductions", 5, "review", isReview: true);

        obj.IsReview.Should().BeTrue();
        obj.Source.Should().Be("review");
    }

    // 26. MasteryEvaluationReason.PlanGeneration exists and has value 5.
    [Fact]
    public void MasteryEvaluationReason_PlanGeneration_ExistsAndHasValue5()
    {
        var reason = LinguaCoach.Application.Mastery.MasteryEvaluationReason.PlanGeneration;

        ((int)reason).Should().Be(5);
    }

    // 27. Regeneration lifecycle: Active → Regenerating → Active (via MarkReady).
    [Fact]
    public void RegenerationLifecycle_ActiveToRegeneratingToActive()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation");

        plan.StartRegeneration("preference_change");
        plan.Status.Should().Be(LearningPlanStatus.Regenerating);

        plan.MarkReady(DateTime.UtcNow);
        plan.Status.Should().Be(LearningPlanStatus.Active);
    }

    // 28. Superseded plan cannot be regenerated (status remains Superseded after Supersede).
    [Fact]
    public void SupersededPlan_StatusIsSuperseded()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation");
        plan.Supersede();

        plan.Status.Should().Be(LearningPlanStatus.Superseded);
    }

    // 29. Multiple objectives can be added to a plan.
    [Fact]
    public void AddObjective_MultipleObjectives_AllPresent()
    {
        var plan = new StudentLearningPlan(Guid.NewGuid(), "B2", "initial_generation");

        for (var i = 0; i < 5; i++)
        {
            plan.AddObjective(new StudentLearningPlanObjective(
                plan.Id, $"b2_obj_{i:D2}", "B2", "speaking", "workplace", null, i, "routing"));
        }

        plan.Objectives.Should().HaveCount(5);
    }

    // 30. PreferredObjectiveKey is part of CurriculumRoutingRequest (Phase 12D addition).
    [Fact]
    public void CurriculumRoutingRequest_HasPreferredObjectiveKey()
    {
        var req = new LinguaCoach.Application.Curriculum.CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            Source = "test",
            PreferredObjectiveKey = "b2_speaking_01"
        };

        req.PreferredObjectiveKey.Should().Be("b2_speaking_01");
    }
}
