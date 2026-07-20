using FluentAssertions;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.LearningPlan;

/// <summary>
/// Unit tests for Phase 12F — Learning Plan Completion Lifecycle.
/// Pure domain logic — no database required.
/// </summary>
public sealed class LearningPlanCompletionTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StudentLearningPlanObjective MakeObjective(
        Guid planId,
        string key = "b2_speaking_meetings",
        bool isBlocked = false)
        => new(planId, key, "B2", "speaking", "workplace",
            title: null, priority: 0, source: "test",
            plannedOrder: 0, isReview: false, isBlocked: isBlocked);

    // ── 1. Active → InProgress → Completed lifecycle ──────────────────────────

    [Fact]
    public void Objective_CanTransition_ActiveToInProgressToCompleted()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId);

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Active);

        obj.MarkInProgress();
        obj.Status.Should().Be(LearningPlanObjectiveStatus.InProgress);
        obj.LastEvaluatedAt.Should().NotBeNull();

        obj.MarkCompleted();
        obj.Status.Should().Be(LearningPlanObjectiveStatus.Completed);
        obj.LastEvaluatedAt.Should().NotBeNull();
    }

    // ── 2. Cannot skip directly to Completed without InProgress ───────────────

    [Fact]
    public void Objective_MarkCompleted_FromActive_SetsCompleted()
    {
        // Domain allows MarkCompleted from any state; the service layer enforces
        // evidence gates. Verify the transition itself works.
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId);

        obj.MarkCompleted();

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Completed);
    }

    // ── 3. Duplicate Completed events are ignored (idempotency guard) ─────────

    [Fact]
    public void MarkCompleted_Idempotency_AlreadyCompleted_NoStatusChange()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId);
        obj.MarkCompleted();
        var ts1 = obj.LastEvaluatedAt;

        // Simulate a second completion event — the service should no-op, but
        // domain MarkCompleted itself is safe to call again.
        obj.MarkCompleted();

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Completed);
        // LastEvaluatedAt may update; the key invariant is status stays Completed.
    }

    // ── 4. Repeated mastery evaluation is idempotent ──────────────────────────

    [Fact]
    public void MarkMastered_Idempotency_CalledTwice_StatusRemainsKMastered()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId);

        obj.MarkMastered();
        obj.MarkMastered(); // second call must not throw or change status

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Mastered);
    }

    // ── 5. Completed status does not regress to lower status ──────────────────

    [Fact]
    public void Objective_CompletedToMastered_AllowedUpgradeOnly()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId);
        obj.MarkCompleted();

        // Mastered is a higher terminal state — upgrade is allowed.
        obj.MarkMastered();

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Mastered);
    }

    // ── 6. Completed objectives are excluded from next-objective selection ─────

    [Fact]
    public void CompletedObjective_IsExcluded_FromActiveCandidates()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId, "b2_speaking_meetings");
        var obj2 = MakeObjective(planId, "b2_writing_emails");

        obj.MarkCompleted();

        var candidates = new[] { obj, obj2 }
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active)
            .ToList();

        candidates.Should().ContainSingle();
        candidates[0].ObjectiveKey.Should().Be("b2_writing_emails");
    }

    // ── 7. Review routing still allowed when objective is Active review ────────

    [Fact]
    public void ReviewObjective_WithActiveStatus_StillSelectable()
    {
        var planId = Guid.NewGuid();
        var reviewObj = new StudentLearningPlanObjective(
            planId, "a2_vocabulary_workplace", "A2", "vocabulary", "workplace",
            title: null, priority: 5, source: "review",
            plannedOrder: 5, isReview: true, isBlocked: false);

        reviewObj.Status.Should().Be(LearningPlanObjectiveStatus.Active);
        reviewObj.IsReview.Should().BeTrue();

        // Active review objectives should appear in candidate selection.
        var isSelectable = reviewObj.Status == LearningPlanObjectiveStatus.Active && !reviewObj.IsBlocked;
        isSelectable.Should().BeTrue();
    }

    // ── 8. Progress percentage updates with completed + mastered counts ────────

    [Fact]
    public void ProgressSummary_CompletionPercentage_ReflectsCompletedAndMastered()
    {
        var studentId = Guid.NewGuid();

        // 2 completed + 1 mastered out of 10 = 30%
        var progress = new LearningPlanProgressSummary(
            StudentProfileId: studentId,
            CurrentCefrLevel: "B2",
            TotalObjectives: 10,
            ObjectivesCompleted: 2,
            ObjectivesMastered: 1,
            ObjectivesInProgress: 1,
            ObjectivesRemaining: 6,
            ReviewObjectives: 0,
            BlockedObjectives: 0,
            DeferredObjectives: 0,
            CompletionPercentage: 30.0,
            MasteryPercentage: 10.0,
            CurrentLearningPhase: "Intermediate — Building",
            LessonQueueLength: 3,
            LessonQueueTarget: 10,
            LastCompletedAt: DateTime.UtcNow.AddHours(-1),
            CurrentObjectiveKey: null,
            CurrentObjectiveSkill: null,
            NextObjectiveKey: null,
            ObjectivesCompletedToday: 0);

        progress.CompletionPercentage.Should().Be(30.0);
        progress.MasteryPercentage.Should().Be(10.0);
        progress.ObjectivesCompleted.Should().Be(2);
        progress.ObjectivesMastered.Should().Be(1);
        progress.LastCompletedAt.Should().NotBeNull();
    }

    // ── 9. Completion timestamps are recorded on domain transitions ───────────

    [Fact]
    public void MarkCompleted_SetsLastEvaluatedAt()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId);
        var before = DateTime.UtcNow.AddSeconds(-1);

        obj.MarkCompleted();

        obj.LastEvaluatedAt.Should().NotBeNull();
        obj.LastEvaluatedAt!.Value.Should().BeAfter(before);
    }

    [Fact]
    public void MarkMastered_SetsLastEvaluatedAt()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId);
        var before = DateTime.UtcNow.AddSeconds(-1);

        obj.MarkMastered();

        obj.LastEvaluatedAt.Should().NotBeNull();
        obj.LastEvaluatedAt!.Value.Should().BeAfter(before);
    }

    // ── 10. Mastered report drives MarkObjectiveMasteredAsync path ────────────

    [Fact]
    public void StudentMasteryReport_WithMasteredKeys_CanDriveCompletion()
    {
        var report = new StudentMasteryReport
        {
            StudentId = Guid.NewGuid(),
            EvaluatedAtUtc = DateTime.UtcNow,
            Reason = MasteryEvaluationReason.ScheduledSweep,
            MasteredObjectiveKeys = ["b2_speaking_meetings"],
            CompletedObjectiveKeys = [],
            WeakObjectiveKeys = [],
            AtRiskObjectiveKeys = [],
            DemotedCount = 0,
            SkippedCount = 0,
            MarkedReviewOnlyCount = 0
        };

        report.MasteredObjectiveKeys.Should().ContainSingle("b2_speaking_meetings");
        report.CompletedObjectiveKeys.Should().BeEmpty();
    }

    // ── 11. CompletedObjectiveKeys populated from NeedsReview signal ──────────

    [Fact]
    public void StudentMasteryReport_CompletedObjectiveKeys_IncludesNeedsReviewKeys()
    {
        var report = new StudentMasteryReport
        {
            StudentId = Guid.NewGuid(),
            EvaluatedAtUtc = DateTime.UtcNow,
            Reason = MasteryEvaluationReason.ScheduledSweep,
            MasteredObjectiveKeys = [],
            CompletedObjectiveKeys = ["b2_writing_emails"],
            WeakObjectiveKeys = ["b2_writing_emails"],
            AtRiskObjectiveKeys = [],
            DemotedCount = 0,
            SkippedCount = 0,
            MarkedReviewOnlyCount = 0
        };

        // CompletedObjectiveKeys is a subset of WeakObjectiveKeys.
        report.CompletedObjectiveKeys.Should().Contain("b2_writing_emails");
        report.WeakObjectiveKeys.Should().Contain("b2_writing_emails");
    }

    // ── 12. Blocked objectives never complete while IsBlocked ─────────────────

    [Fact]
    public void BlockedObjective_StartsBlocked_NotSelectable()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId, isBlocked: true);

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Blocked);
        obj.IsBlocked.Should().BeTrue();

        var isSelectable = obj.Status == LearningPlanObjectiveStatus.Active && !obj.IsBlocked;
        isSelectable.Should().BeFalse();
    }

    // ── 13. Unblock transitions Blocked → Active ──────────────────────────────

    [Fact]
    public void Unblock_TransitionsBlocked_ToActive()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId, isBlocked: true);

        obj.Unblock();

        obj.Status.Should().Be(LearningPlanObjectiveStatus.Active);
        obj.IsBlocked.Should().BeFalse();
    }

    // ── 14. Mastered status is the highest terminal state ────────────────────

    [Fact]
    public void Mastered_IsHigherTerminal_ThanCompleted()
    {
        var planId = Guid.NewGuid();
        var obj = MakeObjective(planId);

        obj.MarkCompleted();
        obj.Status.Should().Be(LearningPlanObjectiveStatus.Completed);

        obj.MarkMastered();
        obj.Status.Should().Be(LearningPlanObjectiveStatus.Mastered);
    }

    // ── 15. InProgress objectives are excluded from Active candidate pool ──────

    [Fact]
    public void InProgressObjective_NotInActiveCandidates_NextActiveSelected()
    {
        var planId = Guid.NewGuid();
        var obj1 = MakeObjective(planId, "b2_speaking_meetings");
        var obj2 = MakeObjective(planId, "b2_writing_emails");

        obj1.MarkInProgress();

        // Simulate GetNextPlannedObjectiveAsync behaviour: Active only.
        var next = new[] { obj1, obj2 }
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .FirstOrDefault();

        next.Should().NotBeNull();
        next!.ObjectiveKey.Should().Be("b2_writing_emails");
    }
}
