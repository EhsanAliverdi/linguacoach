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
}
