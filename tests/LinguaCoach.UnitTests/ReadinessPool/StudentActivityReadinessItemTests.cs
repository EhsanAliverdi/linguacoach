using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.ReadinessPool;

public sealed class StudentActivityReadinessItemTests
{
    private static readonly Guid StudentId = Guid.NewGuid();

    private static StudentActivityReadinessItem MakeQueued(
        RoutingReason reason = RoutingReason.Normal,
        bool isLowerLevel = false,
        string cefr = "B2")
    {
        return new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: cefr,
            routingReason: reason,
            isLowerLevelContent: isLowerLevel,
            primarySkill: "speaking",
            contextTagsJson: "[\"general_english\"]");
    }

    // 1. Can create queued item with routing snapshot.
    [Fact]
    public void Create_SetsQueuedStatus_AndPreservesSnapshot()
    {
        var item = MakeQueued();

        item.Status.Should().Be(ReadinessPoolStatus.Queued);
        item.TargetCefrLevel.Should().Be("B2");
        item.RoutingReason.Should().Be(RoutingReason.Normal);
        item.IsLowerLevelContent.Should().BeFalse();
        item.ContextTagsJson.Should().Contain("general_english");
        item.AttemptCount.Should().Be(0);
    }

    // 2. queued → generating → ready works.
    [Fact]
    public void Lifecycle_QueuedToGeneratingToReady_Works()
    {
        var item = MakeQueued();

        item.MarkGenerating();
        item.Status.Should().Be(ReadinessPoolStatus.Generating);
        item.AttemptCount.Should().Be(1);

        var activityId = Guid.NewGuid();
        item.MarkReady(learningActivityId: activityId);
        item.Status.Should().Be(ReadinessPoolStatus.Ready);
        item.LearningActivityId.Should().Be(activityId);
    }

    // 3. generating → failed works.
    [Fact]
    public void MarkFailed_FromGenerating_SetsFailedStatus()
    {
        var item = MakeQueued();
        item.MarkGenerating();

        item.MarkFailed("ERR_AI", "AI provider unavailable");

        item.Status.Should().Be(ReadinessPoolStatus.Failed);
        item.ErrorCode.Should().Be("ERR_AI");
        item.ErrorMessage.Should().Be("AI provider unavailable");
    }

    // 4. ready → reserved → consumed works.
    [Fact]
    public void Lifecycle_ReadyToReservedToConsumed_Works()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        item.Reserve();
        item.Status.Should().Be(ReadinessPoolStatus.Reserved);
        item.ReservedAt.Should().NotBeNull();

        item.MarkConsumed();
        item.Status.Should().Be(ReadinessPoolStatus.Consumed);
        item.ConsumedAt.Should().NotBeNull();
    }

    // 5. Consumed item cannot be reserved again.
    [Fact]
    public void Reserve_OnConsumedItem_Throws()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.Reserve();
        item.MarkConsumed();

        var act = () => item.Reserve();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Reserve requires status Ready*");
    }

    // 6. Expired item cannot be reserved.
    [Fact]
    public void Reserve_OnExpiredItem_Throws()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.Expire("test expiry");

        var act = () => item.Reserve();
        act.Should().Throw<InvalidOperationException>();
    }

    // 7. stale item cannot be served as normal ready.
    [Fact]
    public void MarkStale_FromReady_SetsStaleStatus_AndIsNotServableAsNormal()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        item.MarkStale("routing changed");

        item.Status.Should().Be(ReadinessPoolStatus.Stale);
        item.IsServableAsNormalContent.Should().BeFalse();
    }

    // 8. review_only item excluded from normal ready query helper.
    [Fact]
    public void MarkReviewOnly_SetsReviewOnlyStatus_NotServableAsNormal()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        item.MarkReviewOnly("mastery passed");

        item.Status.Should().Be(ReadinessPoolStatus.ReviewOnly);
        item.IsServableAsNormalContent.Should().BeFalse();
    }

    // 9. review_only item can be returned by review query.
    [Fact]
    public void ReviewOnly_IsServableAsReview()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.MarkReviewOnly();

        item.IsServableAsReview.Should().BeTrue();
    }

    // 10. lower-level content requires non-normal routing reason.
    [Fact]
    public void Create_LowerLevelWithNormalReason_Throws()
    {
        var act = () => new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: true);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*non-Normal RoutingReason*");
    }

    // 11. B2 student / B1 content stored as lower-level + review/scaffold.
    [Fact]
    public void Create_B1ContentForB2Student_StoredAsLowerLevelWithReview()
    {
        var item = new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Review,
            isLowerLevelContent: true,
            originalCefrLevelSnapshot: "B2");

        item.IsLowerLevelContent.Should().BeTrue();
        item.RoutingReason.Should().Be(RoutingReason.Review);
        item.OriginalCefrLevelSnapshot.Should().Be("B2");
        item.TargetCefrLevel.Should().Be("B1");
    }

    // 12. general_english remains default context (not workplace).
    [Fact]
    public void Create_DefaultContextTags_ContainsGeneralEnglish_NotWorkplace()
    {
        var item = new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B2",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false,
            contextTagsJson: "[\"general_english\"]");

        item.ContextTagsJson.Should().Contain("general_english");
        item.ContextTagsJson.Should().NotContain("workplace");
    }

    // 13. workplace is not set as default when no context provided.
    [Fact]
    public void Create_NoContextTags_DefaultsToEmptyArray_NotWorkplace()
    {
        var item = MakeQueued();
        // contextTagsJson defaults to "[]" when not explicitly passed
        item.ContextTagsJson.Should().NotContain("workplace_english");
    }

    // 14. metadata snapshot survives: original CEFR stored separately.
    [Fact]
    public void Create_OriginalCefrSnapshot_PreservedSeparatelyFromTarget()
    {
        var item = new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.LessonBatch,
            targetCefrLevel: "B2",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false,
            originalCefrLevelSnapshot: "B2+");

        item.TargetCefrLevel.Should().Be("B2");
        item.OriginalCefrLevelSnapshot.Should().Be("B2+");
    }

    // Extra: invalid state transitions throw.
    [Fact]
    public void MarkReady_FromQueued_Throws()
    {
        var item = MakeQueued();
        var act = () => item.MarkReady();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MarkReady requires status Generating*");
    }

    [Fact]
    public void MarkConsumed_FromReady_Throws()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        var act = () => item.MarkConsumed();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MarkConsumed requires status Reserved*");
    }

    [Fact]
    public void Expire_OnConsumed_Throws()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.Reserve();
        item.MarkConsumed();

        var act = () => item.Expire();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkStale_FromQueued_Throws()
    {
        var item = MakeQueued();
        var act = () => item.MarkStale();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MarkStale requires Ready or Reserved*");
    }

    [Fact]
    public void Create_EmptyStudentId_Throws()
    {
        var act = () => new StudentActivityReadinessItem(
            studentId: Guid.Empty,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B2",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false);

        act.Should().Throw<ArgumentException>().WithMessage("*StudentId*");
    }

    [Fact]
    public void LinkMaterializedIds_UpdatesIds()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        var sessionId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        item.LinkMaterializedIds(sessionId, activityId, null);

        item.LearningSessionId.Should().Be(sessionId);
        item.LearningActivityId.Should().Be(activityId);
    }

    // --- Skipped status tests ---

    // 15. Ready item can be marked Skipped (mastered objective).
    [Fact]
    public void MarkSkipped_FromReady_SetsSkippedStatus()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        item.MarkSkipped("objective already mastered");

        item.Status.Should().Be(ReadinessPoolStatus.Skipped);
        item.ErrorMessage.Should().Be("objective already mastered");
        item.IsServableAsNormalContent.Should().BeFalse();
        item.IsServableAsReview.Should().BeFalse();
    }

    // 16. Reserved item can be marked Skipped.
    [Fact]
    public void MarkSkipped_FromReserved_SetsSkippedStatus()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.Reserve();

        item.MarkSkipped("profile no longer matches");

        item.Status.Should().Be(ReadinessPoolStatus.Skipped);
    }

    // 17. ReviewOnly item can be marked Skipped.
    [Fact]
    public void MarkSkipped_FromReviewOnly_SetsSkippedStatus()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.MarkReviewOnly("passed");

        item.MarkSkipped("no longer useful");

        item.Status.Should().Be(ReadinessPoolStatus.Skipped);
    }

    // 18. Consumed item cannot be skipped.
    [Fact]
    public void MarkSkipped_FromConsumed_Throws()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.Reserve();
        item.MarkConsumed();

        var act = () => item.MarkSkipped();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*terminal status*");
    }

    // 19. Already-skipped item cannot be skipped again.
    [Fact]
    public void MarkSkipped_AlreadySkipped_Throws()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.MarkSkipped();

        var act = () => item.MarkSkipped();
        act.Should().Throw<InvalidOperationException>();
    }

    // 20. Expired item cannot be skipped.
    [Fact]
    public void MarkSkipped_FromExpired_Throws()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();
        item.Expire();

        var act = () => item.MarkSkipped();
        act.Should().Throw<InvalidOperationException>();
    }

    // --- RecordEvaluation / LastEvaluatedAtUtc tests ---

    // 21. RecordEvaluation stamps LastEvaluatedAtUtc.
    [Fact]
    public void RecordEvaluation_SetsLastEvaluatedAtUtc()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        item.LastEvaluatedAtUtc.Should().BeNull();
        item.RecordEvaluation();
        item.LastEvaluatedAtUtc.Should().NotBeNull();
        item.LastEvaluatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // 22. RecordEvaluation does not change lifecycle status.
    [Fact]
    public void RecordEvaluation_DoesNotChangeStatus()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        item.RecordEvaluation();

        item.Status.Should().Be(ReadinessPoolStatus.Ready);
    }

    // --- Phase 19B: per-item admin approval ---

    private static StudentActivityReadinessItem MakeReadyReviewScaffoldItem()
    {
        var item = new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Scaffold,
            isLowerLevelContent: true,
            originalCefrLevelSnapshot: "B2",
            requiresAdminReview: true);
        item.MarkGenerating();
        item.MarkReady();
        return item;
    }

    // 23. Creating with requiresAdminReview=true stamps AdminReviewStatus=PendingReview.
    [Fact]
    public void Create_WithRequiresAdminReview_SetsAdminReviewStatusPendingReview()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.AdminReviewStatus.Should().Be(AdminReviewStatus.PendingReview);
        item.PassesAdminReviewGate.Should().BeFalse();
    }

    // 24. Normal items (requiresAdminReview=false) default to NotRequired and pass the gate.
    [Fact]
    public void Create_WithoutRequiresAdminReview_SetsAdminReviewStatusNotRequired()
    {
        var item = MakeQueued();
        item.AdminReviewStatus.Should().Be(AdminReviewStatus.NotRequired);
        item.PassesAdminReviewGate.Should().BeTrue();
    }

    // 25. Approve moves PendingReview -> Approved and stamps reviewer/timestamp.
    [Fact]
    public void ApproveAdminReview_FromPendingReview_SetsApprovedAndStampsReviewer()
    {
        var item = MakeReadyReviewScaffoldItem();
        var adminId = Guid.NewGuid();

        item.ApproveAdminReview(adminId, "looks fine");

        item.AdminReviewStatus.Should().Be(AdminReviewStatus.Approved);
        item.AdminReviewedByUserId.Should().Be(adminId);
        item.AdminReviewedAtUtc.Should().NotBeNull();
        item.AdminReviewNotes.Should().Be("looks fine");
        item.PassesAdminReviewGate.Should().BeTrue();
    }

    // 26. Approve is idempotent when already Approved.
    [Fact]
    public void ApproveAdminReview_AlreadyApproved_IsIdempotent()
    {
        var item = MakeReadyReviewScaffoldItem();
        var adminId = Guid.NewGuid();
        item.ApproveAdminReview(adminId);
        var firstTimestamp = item.AdminReviewedAtUtc;

        item.ApproveAdminReview(Guid.NewGuid());

        item.AdminReviewStatus.Should().Be(AdminReviewStatus.Approved);
        item.AdminReviewedByUserId.Should().Be(adminId);
        item.AdminReviewedAtUtc.Should().Be(firstTimestamp);
    }

    // 27. Cannot approve an Expired item.
    [Fact]
    public void ApproveAdminReview_ExpiredItem_Throws()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.Expire();

        var act = () => item.ApproveAdminReview(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    // 28. Cannot approve a Failed item.
    [Fact]
    public void ApproveAdminReview_FailedItem_Throws()
    {
        var item = new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Scaffold,
            isLowerLevelContent: true,
            originalCefrLevelSnapshot: "B2",
            requiresAdminReview: true);
        item.MarkGenerating();
        item.MarkFailed("ERR_AI", "boom");

        var act = () => item.ApproveAdminReview(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    // 29. Cannot approve a Stale item.
    [Fact]
    public void ApproveAdminReview_StaleItem_Throws()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.MarkStale("profile changed");

        var act = () => item.ApproveAdminReview(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    // 30. Reject requires a non-empty reason.
    [Fact]
    public void RejectAdminReview_EmptyReason_Throws()
    {
        var item = MakeReadyReviewScaffoldItem();
        var act = () => item.RejectAdminReview(Guid.NewGuid(), "");
        act.Should().Throw<ArgumentException>();
    }

    // 31. Reject from PendingReview sets Rejected + persists reason/notes.
    [Fact]
    public void RejectAdminReview_FromPendingReview_PersistsReasonAndNotes()
    {
        var item = MakeReadyReviewScaffoldItem();
        var adminId = Guid.NewGuid();

        item.RejectAdminReview(adminId, "Too hard for level", "double-checked with mastery report");

        item.AdminReviewStatus.Should().Be(AdminReviewStatus.Rejected);
        item.AdminReviewedByUserId.Should().Be(adminId);
        item.AdminReviewReason.Should().Be("Too hard for level");
        item.AdminReviewNotes.Should().Be("double-checked with mastery report");
        item.PassesAdminReviewGate.Should().BeFalse();
    }

    // 32. Reject is idempotent when already Rejected.
    [Fact]
    public void RejectAdminReview_AlreadyRejected_IsIdempotent()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.RejectAdminReview(Guid.NewGuid(), "first reason");
        var firstReason = item.AdminReviewReason;

        item.RejectAdminReview(Guid.NewGuid(), "second reason");

        item.AdminReviewStatus.Should().Be(AdminReviewStatus.Rejected);
        item.AdminReviewReason.Should().Be(firstReason);
    }

    // 33. Approved-but-consumed item cannot be rejected.
    [Fact]
    public void RejectAdminReview_ApprovedAndConsumed_Throws()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.ApproveAdminReview(Guid.NewGuid());
        item.Reserve();
        item.MarkConsumed();

        var act = () => item.RejectAdminReview(Guid.NewGuid(), "too late");
        act.Should().Throw<InvalidOperationException>();
    }

    // 34. Approved-but-not-consumed item CAN be rejected.
    [Fact]
    public void RejectAdminReview_ApprovedNotConsumed_Succeeds()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.ApproveAdminReview(Guid.NewGuid());

        item.RejectAdminReview(Guid.NewGuid(), "changed my mind");

        item.AdminReviewStatus.Should().Be(AdminReviewStatus.Rejected);
    }

    // 35. Cannot reject an item that never required review.
    [Fact]
    public void RejectAdminReview_NotRequiredItem_Throws()
    {
        var item = MakeQueued();
        item.MarkGenerating();
        item.MarkReady();

        var act = () => item.RejectAdminReview(Guid.NewGuid(), "n/a");
        act.Should().Throw<InvalidOperationException>();
    }

    // 36. Reopen moves Rejected -> PendingReview.
    [Fact]
    public void ReopenAdminReview_FromRejected_SetsPendingReview()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.RejectAdminReview(Guid.NewGuid(), "too hard");

        item.ReopenAdminReview(Guid.NewGuid(), "reconsidered");

        item.AdminReviewStatus.Should().Be(AdminReviewStatus.PendingReview);
        item.AdminReviewReason.Should().BeNull();
    }

    // 37. Reopen is idempotent when already PendingReview.
    [Fact]
    public void ReopenAdminReview_AlreadyPending_IsIdempotent()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.ReopenAdminReview(Guid.NewGuid());
        item.AdminReviewStatus.Should().Be(AdminReviewStatus.PendingReview);
    }

    // 38. Reopen only allowed from Rejected (not from Approved or NotRequired).
    [Fact]
    public void ReopenAdminReview_FromApproved_Throws()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.ApproveAdminReview(Guid.NewGuid());

        var act = () => item.ReopenAdminReview(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    // 39. Cannot reopen a consumed item (defence-in-depth: AdminReviewStatus and lifecycle
    // Status are tracked independently, so a Rejected item can still be Reserved/Consumed
    // by other code paths).
    [Fact]
    public void ReopenAdminReview_ConsumedItem_Throws()
    {
        var item = MakeReadyReviewScaffoldItem();
        item.RejectAdminReview(Guid.NewGuid(), "hold");
        item.Reserve();
        item.MarkConsumed();

        var act = () => item.ReopenAdminReview(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    // 40. Approving does not touch lifecycle Status, CEFR fields, or linked entities.
    [Fact]
    public void ApproveAdminReview_DoesNotMutateLifecycleOrCefrFields()
    {
        var item = MakeReadyReviewScaffoldItem();
        var statusBefore = item.Status;
        var cefrBefore = item.TargetCefrLevel;

        item.ApproveAdminReview(Guid.NewGuid());

        item.Status.Should().Be(statusBefore);
        item.TargetCefrLevel.Should().Be(cefrBefore);
    }

    // 41. Subskill matching PrimarySkill is accepted.
    [Fact]
    public void Create_SubskillMatchingPrimarySkill_Accepted()
    {
        var item = new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B2",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false,
            primarySkill: "speaking",
            subskill: LinguaCoach.Domain.Constants.CurriculumSubskillConstants.SpeakingRoleplay);

        item.Subskill.Should().Be(LinguaCoach.Domain.Constants.CurriculumSubskillConstants.SpeakingRoleplay);
    }

    // 42. Subskill not belonging to PrimarySkill throws.
    [Fact]
    public void Create_SubskillNotMatchingPrimarySkill_Throws()
    {
        var act = () => new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B2",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false,
            primarySkill: "speaking",
            subskill: LinguaCoach.Domain.Constants.CurriculumSubskillConstants.WritingEmailMessage);

        act.Should().Throw<ArgumentException>();
    }

    // 43. SetTemplateProvenance sets all provenance fields.
    [Fact]
    public void SetTemplateProvenance_SetsAllFields()
    {
        var item = MakeQueued();
        var templateId = Guid.NewGuid();

        item.SetTemplateProvenance(
            sourceTemplateId: templateId,
            formIoSchemaSnapshotJson: "{\"components\":[]}",
            scoringRulesSnapshotJson: "{\"components\":{}}",
            personalizationReason: "Personalized for topic X",
            generatedByModel: "gemini-1.5-pro",
            generatedByProvider: "gemini",
            validationStatus: ActivityValidationStatus.Passed);

        item.SourceTemplateId.Should().Be(templateId);
        item.FormIoSchemaSnapshotJson.Should().Be("{\"components\":[]}");
        item.ScoringRulesSnapshotJson.Should().Be("{\"components\":{}}");
        item.PersonalizationReason.Should().Be("Personalized for topic X");
        item.GeneratedByModel.Should().Be("gemini-1.5-pro");
        item.GeneratedByProvider.Should().Be("gemini");
        item.ValidationStatus.Should().Be(ActivityValidationStatus.Passed);
    }

    // 44. SetTemplateProvenance with empty templateId throws.
    [Fact]
    public void SetTemplateProvenance_EmptyTemplateId_Throws()
    {
        var item = MakeQueued();
        var act = () => item.SetTemplateProvenance(
            Guid.Empty, null, null, null, null, null, ActivityValidationStatus.Passed);

        act.Should().Throw<ArgumentException>();
    }

    // 45. SetBankItemProvenance sets SourceBankItemId.
    [Fact]
    public void SetBankItemProvenance_SetsSourceBankItemId()
    {
        var item = MakeQueued();
        var bankItemId = Guid.NewGuid();

        item.SetBankItemProvenance(bankItemId);

        item.SourceBankItemId.Should().Be(bankItemId);
    }

    // 46. SetBankItemProvenance with empty id throws.
    [Fact]
    public void SetBankItemProvenance_EmptyId_Throws()
    {
        var item = MakeQueued();
        var act = () => item.SetBankItemProvenance(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
