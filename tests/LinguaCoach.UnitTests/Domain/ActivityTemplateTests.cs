using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class ActivityTemplateTests
{
    private static ActivityTemplate MakeTemplate() =>
        new("b1.speaking.roleplay_ordering", "speaking", "B1", "roleplay");

    [Fact]
    public void Constructor_ValidInput_CreatesTemplate()
    {
        var t = MakeTemplate();

        Assert.Equal("b1.speaking.roleplay_ordering", t.Key);
        Assert.Equal("speaking", t.Skill);
        Assert.Equal("B1", t.CefrLevel);
        Assert.Equal("roleplay", t.ActivityType);
        Assert.Equal(1, t.VersionNumber);
        Assert.Null(t.PreviousVersionId);
        Assert.Equal(AdminReviewStatus.NotRequired, t.ReviewStatus);
        Assert.False(t.IsPublished);
        Assert.Equal("[]", t.ContextTagsJson);
        Assert.Equal("[]", t.FocusTagsJson);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_Throws(string key)
    {
        Assert.Throws<ArgumentException>(() => new ActivityTemplate(key, "speaking", "B1", "roleplay"));
    }

    [Fact]
    public void Constructor_InvalidSkill_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ActivityTemplate("k", "not_a_skill", "B1", "roleplay"));
    }

    [Fact]
    public void Constructor_InvalidCefrLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ActivityTemplate("k", "speaking", "X1", "roleplay"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyActivityType_Throws(string activityType)
    {
        Assert.Throws<ArgumentException>(() => new ActivityTemplate("k", "speaking", "B1", activityType));
    }

    [Fact]
    public void Constructor_SubskillMatchingSkill_Accepted()
    {
        var t = new ActivityTemplate("k", "speaking", "B1", "roleplay",
            subskill: CurriculumSubskillConstants.SpeakingRoleplay);

        Assert.Equal(CurriculumSubskillConstants.SpeakingRoleplay, t.Subskill);
    }

    [Fact]
    public void Constructor_SubskillNotMatchingSkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ActivityTemplate("k", "speaking", "B1", "roleplay",
                subskill: CurriculumSubskillConstants.WritingEmailMessage));
    }

    [Fact]
    public void Constructor_NegativeEstimatedDuration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActivityTemplate("k", "speaking", "B1", "roleplay", estimatedDurationSeconds: -1));
    }

    [Fact]
    public void Update_ValidInput_UpdatesAuthorableFields()
    {
        var t = MakeTemplate();
        t.Update("writing", "B2", "email_reply", null, null, "[]", "[]", null, 300, null);

        Assert.Equal("writing", t.Skill);
        Assert.Equal("B2", t.CefrLevel);
        Assert.Equal("email_reply", t.ActivityType);
        Assert.Equal(300, t.EstimatedDurationSeconds);
    }

    [Fact]
    public void Update_DoesNotTouchSchemaOrReviewOrPublishState()
    {
        var t = MakeTemplate();
        t.SetSchema("{}", "{}", null, "instructions");
        t.Approve();
        t.Publish();

        t.Update("writing", "B2", "email_reply", null, null, "[]", "[]", null, null, null);

        Assert.Equal("{}", t.FormIoBaseSchemaJson);
        Assert.Equal(AdminReviewStatus.Approved, t.ReviewStatus);
        Assert.True(t.IsPublished);
    }

    [Fact]
    public void SetSchema_SetsAllSchemaFields()
    {
        var t = MakeTemplate();
        t.SetSchema("{\"components\":[]}", "{\"rules\":[]}", "{\"validate\":true}", "Generate a roleplay.");

        Assert.Equal("{\"components\":[]}", t.FormIoBaseSchemaJson);
        Assert.Equal("{\"rules\":[]}", t.ScoringModelJson);
        Assert.Equal("{\"validate\":true}", t.ValidationRulesJson);
        Assert.Equal("Generate a roleplay.", t.GenerationInstructions);
    }

    [Fact]
    public void Approve_SetsReviewStatusApproved()
    {
        var t = MakeTemplate();
        t.Approve();
        Assert.Equal(AdminReviewStatus.Approved, t.ReviewStatus);
    }

    [Fact]
    public void Reject_SetsReviewStatusRejected_AndUnpublishes()
    {
        var t = MakeTemplate();
        t.Approve();
        t.Publish();

        t.Reject("Not level-appropriate.");

        Assert.Equal(AdminReviewStatus.Rejected, t.ReviewStatus);
        Assert.False(t.IsPublished);
    }

    [Fact]
    public void Reject_EmptyReason_Throws()
    {
        var t = MakeTemplate();
        Assert.Throws<ArgumentException>(() => t.Reject(""));
    }

    [Fact]
    public void ResetToPendingReview_SetsReviewStatusPendingReview()
    {
        var t = MakeTemplate();
        t.Reject("bad");
        t.ResetToPendingReview();
        Assert.Equal(AdminReviewStatus.PendingReview, t.ReviewStatus);
    }

    [Fact]
    public void Publish_WhenRejected_Throws()
    {
        var t = MakeTemplate();
        t.Reject("bad");
        Assert.Throws<InvalidOperationException>(() => t.Publish());
    }

    [Fact]
    public void Publish_WhenNotRejected_Succeeds()
    {
        var t = MakeTemplate();
        t.Publish();
        Assert.True(t.IsPublished);
    }

    [Fact]
    public void Unpublish_SetsIsPublishedFalse()
    {
        var t = MakeTemplate();
        t.Publish();
        t.Unpublish();
        Assert.False(t.IsPublished);
    }
}
