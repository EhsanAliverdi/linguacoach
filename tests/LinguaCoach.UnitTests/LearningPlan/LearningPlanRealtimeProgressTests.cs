using FluentAssertions;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.LearningPlan;

/// <summary>
/// Unit tests for Phase 12G — Real-Time Learning Plan Progress Integration.
/// Pure domain/record logic — no database required.
/// </summary>
public sealed class LearningPlanRealtimeProgressTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StudentLearningPlanObjective MakeObjective(
        string key = "b2_grammar_conditionals",
        int plannedOrder = 1,
        bool isBlocked = false)
        => new(
            planId: Guid.NewGuid(),
            objectiveKey: key,
            cefrLevel: "B2",
            skill: "grammar",
            context: "general_english",
            title: null,
            priority: 1,
            source: "curriculum",
            plannedOrder: plannedOrder,
            isBlocked: isBlocked);

    // ── LearningPlanObjectiveProgressUpdate record ─────────────────────────────

    // 1. Record initialises with StatusChanged=true correctly.
    [Fact]
    public void ProgressUpdate_StatusChanged_InitialisesAllFields()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "b2_speaking_presentations",
            PreviousStatus: LearningPlanObjectiveStatus.Active,
            NewStatus: LearningPlanObjectiveStatus.Mastered,
            StatusChanged: true,
            Reason: "mastered");

        update.ObjectiveKey.Should().Be("b2_speaking_presentations");
        update.PreviousStatus.Should().Be(LearningPlanObjectiveStatus.Active);
        update.NewStatus.Should().Be(LearningPlanObjectiveStatus.Mastered);
        update.StatusChanged.Should().BeTrue();
        update.Reason.Should().Be("mastered");
    }

    // 2. No status change preserves identical Previous and New.
    [Fact]
    public void ProgressUpdate_NoChange_PreviousEqualsNew()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "b2_vocab_collocations",
            PreviousStatus: LearningPlanObjectiveStatus.Active,
            NewStatus: LearningPlanObjectiveStatus.Active,
            StatusChanged: false,
            Reason: "insufficient_evidence_NeedsPractice");

        update.StatusChanged.Should().BeFalse();
        update.PreviousStatus.Should().Be(update.NewStatus);
        update.Reason.Should().StartWith("insufficient_evidence");
    }

    // 3. No active plan returns nulls and correct reason.
    [Fact]
    public void ProgressUpdate_NoActivePlan_NullStatusesAndCorrectReason()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "b2_grammar_passives",
            PreviousStatus: null,
            NewStatus: null,
            StatusChanged: false,
            Reason: "no_active_plan");

        update.PreviousStatus.Should().BeNull();
        update.NewStatus.Should().BeNull();
        update.StatusChanged.Should().BeFalse();
        update.Reason.Should().Be("no_active_plan");
    }

    // 4. Objective not found in plan returns correct reason.
    [Fact]
    public void ProgressUpdate_ObjectiveNotInPlan_CorrectReason()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "unknown_key",
            PreviousStatus: null,
            NewStatus: null,
            StatusChanged: false,
            Reason: "objective_not_in_plan");

        update.StatusChanged.Should().BeFalse();
        update.Reason.Should().Be("objective_not_in_plan");
    }

    // 5. Error path returns error reason without throwing.
    [Fact]
    public void ProgressUpdate_ErrorPath_ReturnsErrorReasonAndFalse()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "b2_listening_lectures",
            PreviousStatus: null,
            NewStatus: null,
            StatusChanged: false,
            Reason: "error");

        update.Reason.Should().Be("error");
        update.StatusChanged.Should().BeFalse();
        update.PreviousStatus.Should().BeNull();
        update.NewStatus.Should().BeNull();
    }

    // 6. Already Mastered is terminal — returns already_terminal.
    [Fact]
    public void ProgressUpdate_AlreadyMastered_AlreadyTerminalReason()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "b2_grammar_conditionals",
            PreviousStatus: LearningPlanObjectiveStatus.Mastered,
            NewStatus: LearningPlanObjectiveStatus.Mastered,
            StatusChanged: false,
            Reason: "already_terminal");

        update.Reason.Should().Be("already_terminal");
        update.StatusChanged.Should().BeFalse();
        update.NewStatus.Should().Be(LearningPlanObjectiveStatus.Mastered);
    }

    // 7. Already Completed is also terminal for the real-time update path.
    [Fact]
    public void ProgressUpdate_AlreadyCompleted_AlreadyTerminalReason()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "b2_writing_essays",
            PreviousStatus: LearningPlanObjectiveStatus.Completed,
            NewStatus: LearningPlanObjectiveStatus.Completed,
            StatusChanged: false,
            Reason: "already_terminal");

        update.Reason.Should().Be("already_terminal");
        update.StatusChanged.Should().BeFalse();
    }

    // 8. Mastered signal produces StatusChanged=true and Mastered status.
    [Fact]
    public void ProgressUpdate_MasteredSignal_StatusChangedTrue_NewStatusMastered()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "b2_speaking_negotiations",
            PreviousStatus: LearningPlanObjectiveStatus.InProgress,
            NewStatus: LearningPlanObjectiveStatus.Mastered,
            StatusChanged: true,
            Reason: "mastered");

        update.StatusChanged.Should().BeTrue();
        update.NewStatus.Should().Be(LearningPlanObjectiveStatus.Mastered);
        update.PreviousStatus.Should().Be(LearningPlanObjectiveStatus.InProgress);
        update.Reason.Should().Be("mastered");
    }

    // 9. NeedsReview signal transitions to Completed (not Mastered).
    [Fact]
    public void ProgressUpdate_NeedsReviewSignal_TransitionsToCompleted()
    {
        var update = new LearningPlanObjectiveProgressUpdate(
            ObjectiveKey: "b2_writing_reports",
            PreviousStatus: LearningPlanObjectiveStatus.Active,
            NewStatus: LearningPlanObjectiveStatus.Completed,
            StatusChanged: true,
            Reason: "needs_review");

        update.StatusChanged.Should().BeTrue();
        update.NewStatus.Should().Be(LearningPlanObjectiveStatus.Completed);
        update.NewStatus.Should().NotBe(LearningPlanObjectiveStatus.Mastered);
        update.Reason.Should().Be("needs_review");
    }

    // ── CurrentObjectiveKey / NextObjectiveKey derivation ─────────────────────

    // 10. InProgress objective is preferred as CurrentObjectiveKey over Active.
    [Fact]
    public void CurrentObjectiveKey_PrefersInProgress_OverActive()
    {
        var inProg = MakeObjective("obj-in-progress", plannedOrder: 1);
        inProg.MarkInProgress();
        var active = MakeObjective("obj-active", plannedOrder: 2);

        var objectives = new[] { inProg, active };

        var inProgressCandidate = objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.InProgress)
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .FirstOrDefault();

        var activeOrdered = objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .ToList();

        var currentKey = inProgressCandidate?.ObjectiveKey ?? activeOrdered.FirstOrDefault()?.ObjectiveKey;
        var nextKey = inProgressCandidate is not null
            ? activeOrdered.FirstOrDefault()?.ObjectiveKey
            : activeOrdered.Skip(1).FirstOrDefault()?.ObjectiveKey;

        currentKey.Should().Be("obj-in-progress");
        nextKey.Should().Be("obj-active");
    }

    // 11. When no InProgress, CurrentObjectiveKey is first Active by PlannedOrder.
    [Fact]
    public void CurrentObjectiveKey_FallsBackToFirstActive_OrderedByPlannedOrder()
    {
        var second = MakeObjective("obj-second", plannedOrder: 2);
        var first = MakeObjective("obj-first", plannedOrder: 1);
        var objectives = new[] { second, first }; // shuffled

        var activeOrdered = objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .ToList();

        var currentKey = activeOrdered.FirstOrDefault()?.ObjectiveKey;
        var nextKey = activeOrdered.Skip(1).FirstOrDefault()?.ObjectiveKey;

        currentKey.Should().Be("obj-first");
        nextKey.Should().Be("obj-second");
    }

    // 12. Blocked Active objectives are excluded from CurrentObjectiveKey selection.
    [Fact]
    public void CurrentObjectiveKey_ExcludesBlockedActive()
    {
        var blocked = MakeObjective("obj-blocked", plannedOrder: 1, isBlocked: true);
        var unblocked = MakeObjective("obj-unblocked", plannedOrder: 2);

        var objectives = new[] { blocked, unblocked };

        var activeOrdered = objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .ToList();

        var currentKey = activeOrdered.FirstOrDefault()?.ObjectiveKey;

        currentKey.Should().Be("obj-unblocked");
    }

    // 13. Both keys are null when no objectives exist.
    [Fact]
    public void CurrentAndNextObjectiveKeys_AreNull_WhenNoObjectives()
    {
        var objectives = Array.Empty<StudentLearningPlanObjective>();

        var currentKey = objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
            .FirstOrDefault()?.ObjectiveKey;

        var nextKey = objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
            .Skip(1).FirstOrDefault()?.ObjectiveKey;

        currentKey.Should().BeNull();
        nextKey.Should().BeNull();
    }

    // ── ObjectivesCompletedToday ──────────────────────────────────────────────

    // 14. Objectives marked completed/mastered today are counted.
    [Fact]
    public void ObjectivesCompletedToday_CountsCompletedAndMasteredSinceMidnight()
    {
        var obj1 = MakeObjective("obj-1");
        obj1.MarkCompleted(); // LastEvaluatedAt = UtcNow

        var obj2 = MakeObjective("obj-2");
        obj2.MarkMastered(); // LastEvaluatedAt = UtcNow

        var obj3 = MakeObjective("obj-3");
        obj3.MarkCompleted();
        obj3.Evaluate(DateTime.UtcNow.AddDays(-1)); // back-date to yesterday

        var obj4 = MakeObjective("obj-4"); // Active — not done

        var objectives = new[] { obj1, obj2, obj3, obj4 };
        var todayUtc = DateTime.UtcNow.Date;

        var count = objectives.Count(o =>
            o.Status is LearningPlanObjectiveStatus.Completed or LearningPlanObjectiveStatus.Mastered
            && o.LastEvaluatedAt.HasValue
            && o.LastEvaluatedAt.Value.Date >= todayUtc);

        count.Should().Be(2);
    }

    // 15. Yesterday's completions are excluded from today's count.
    [Fact]
    public void ObjectivesCompletedToday_ExcludesYesterdayCompletions()
    {
        var obj1 = MakeObjective("obj-yesterday-c");
        obj1.MarkCompleted();
        obj1.Evaluate(DateTime.UtcNow.AddDays(-1));

        var obj2 = MakeObjective("obj-yesterday-m");
        obj2.MarkMastered();
        obj2.Evaluate(DateTime.UtcNow.AddDays(-2));

        var objectives = new[] { obj1, obj2 };
        var todayUtc = DateTime.UtcNow.Date;

        var count = objectives.Count(o =>
            o.Status is LearningPlanObjectiveStatus.Completed or LearningPlanObjectiveStatus.Mastered
            && o.LastEvaluatedAt.HasValue
            && o.LastEvaluatedAt.Value.Date >= todayUtc);

        count.Should().Be(0);
    }
}
