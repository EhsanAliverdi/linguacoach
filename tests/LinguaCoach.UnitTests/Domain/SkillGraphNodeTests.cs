using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class SkillGraphNodeTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesNodeAsPendingReview()
    {
        var node = new SkillGraphNode(
            key: "grammar.present_simple.a1",
            title: "Present simple for daily routines",
            description: "Teaches present simple for habitual actions.",
            cefrLevel: "A1",
            skill: "grammar");

        Assert.Equal("grammar.present_simple.a1", node.Key);
        Assert.Equal("A1", node.CefrLevel);
        Assert.Equal("grammar", node.Skill);
        Assert.Equal(AdminReviewStatus.PendingReview, node.ReviewStatus);
        Assert.True(node.IsActive);
        Assert.Equal(1, node.DifficultyBand);
    }

    [Fact]
    public void Constructor_InvalidCefrLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SkillGraphNode("k", "T", "D", "Z9", "grammar"));
    }

    [Fact]
    public void Constructor_InvalidSkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SkillGraphNode("k", "T", "D", "A1", "not_a_skill"));
    }

    [Fact]
    public void Constructor_SubskillNotBelongingToSkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SkillGraphNode("k", "T", "D", "A1", "grammar", subskill: "reading.gist"));
    }

    [Fact]
    public void Constructor_DifficultyBandOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SkillGraphNode("k", "T", "D", "A1", "grammar", difficultyBand: 6));
    }

    [Fact]
    public void Approve_SetsApprovedStatusAndTimestamp()
    {
        var node = new SkillGraphNode("k", "T", "D", "A1", "grammar");
        var userId = Guid.NewGuid();

        node.Approve(userId);

        Assert.Equal(AdminReviewStatus.Approved, node.ReviewStatus);
        Assert.Equal(userId, node.ReviewedByUserId);
        Assert.NotNull(node.ApprovedAtUtc);
        Assert.Null(node.RejectedAtUtc);
    }

    [Fact]
    public void Reject_RequiresReason()
    {
        var node = new SkillGraphNode("k", "T", "D", "A1", "grammar");
        Assert.Throws<ArgumentException>(() => node.Reject("", Guid.NewGuid()));
    }

    [Fact]
    public void Reject_SetsRejectedStatusAndReason()
    {
        var node = new SkillGraphNode("k", "T", "D", "A1", "grammar");
        node.Reject("Too broad.", Guid.NewGuid());

        Assert.Equal(AdminReviewStatus.Rejected, node.ReviewStatus);
        Assert.Equal("Too broad.", node.RejectionReason);
        Assert.NotNull(node.RejectedAtUtc);
    }

    [Fact]
    public void Deactivate_ThenActivate_TogglesIsActive()
    {
        var node = new SkillGraphNode("k", "T", "D", "A1", "grammar");
        node.Deactivate();
        Assert.False(node.IsActive);
        node.Activate();
        Assert.True(node.IsActive);
    }

    [Fact]
    public void UpdateCore_WhilePendingReview_UpdatesFields()
    {
        var node = new SkillGraphNode("k", "Old title", "Old desc", "A1", "grammar");

        node.UpdateCore("New title", "New desc", "A2", "reading", "reading.gist", 3, "AI context");

        Assert.Equal("New title", node.Title);
        Assert.Equal("New desc", node.Description);
        Assert.Equal("A2", node.CefrLevel);
        Assert.Equal("reading", node.Skill);
        Assert.Equal("reading.gist", node.Subskill);
        Assert.Equal(3, node.DifficultyBand);
        Assert.Equal("AI context", node.DescriptionForAi);
        Assert.Equal("k", node.Key); // Key is stable, never edited
    }

    [Fact]
    public void UpdateCore_WhileApproved_Throws()
    {
        var node = new SkillGraphNode("k", "T", "D", "A1", "grammar");
        node.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            node.UpdateCore("New", "New desc", "A1", "grammar", null, 1, null));
    }

    [Fact]
    public void UpdateCore_AfterReject_ResubmitsToPendingReview()
    {
        var node = new SkillGraphNode("k", "T", "D", "A1", "grammar");
        node.Reject("Needs work.", Guid.NewGuid());

        node.UpdateCore("Fixed title", "Fixed desc", "A1", "grammar", null, 1, null);

        Assert.Equal(AdminReviewStatus.PendingReview, node.ReviewStatus);
        Assert.Null(node.RejectionReason);
    }

    [Fact]
    public void UpdateCore_InvalidCefrLevel_Throws()
    {
        var node = new SkillGraphNode("k", "T", "D", "A1", "grammar");
        Assert.Throws<ArgumentException>(() =>
            node.UpdateCore("T", "D", "Z9", "grammar", null, 1, null));
    }
}
