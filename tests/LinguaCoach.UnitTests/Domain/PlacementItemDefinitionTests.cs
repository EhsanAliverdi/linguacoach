using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class PlacementItemDefinitionTests
{
    [Fact]
    public void Constructor_NullSubskill_Accepted()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1);
        Assert.Null(item.Subskill);
    }

    [Fact]
    public void Constructor_ValidSubskillMatchingSkill_Accepted()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1,
            subskill: CurriculumSubskillConstants.SpeakingRoleplay);

        Assert.Equal(CurriculumSubskillConstants.SpeakingRoleplay, item.Subskill);
    }

    [Fact]
    public void Constructor_SubskillNotBelongingToSkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PlacementItemDefinition("speaking", "A1", 1,
                subskill: CurriculumSubskillConstants.WritingEmailMessage));
    }

    [Fact]
    public void SetSubskill_ValidValue_Updates()
    {
        var item = new PlacementItemDefinition("reading", "B1", 1);
        item.SetSubskill(CurriculumSubskillConstants.ReadingGist);
        Assert.Equal(CurriculumSubskillConstants.ReadingGist, item.Subskill);
    }

    [Fact]
    public void SetSubskill_MismatchedSkill_Throws()
    {
        var item = new PlacementItemDefinition("reading", "B1", 1);
        Assert.Throws<ArgumentException>(() =>
            item.SetSubskill(CurriculumSubskillConstants.SpeakingRoleplay));
    }

    [Fact]
    public void Update_DoesNotResetPreviouslySetSubskill()
    {
        var item = new PlacementItemDefinition("reading", "B1", 1,
            subskill: CurriculumSubskillConstants.ReadingGist);

        item.Update("reading", "B1", 2, true);

        Assert.Equal(CurriculumSubskillConstants.ReadingGist, item.Subskill);
    }

    // ── Calibration (Phase 7) ────────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultsCalibrationFields()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1);

        Assert.Equal(1, item.DifficultyBand);
        Assert.Equal(1.0, item.EvidenceWeight);
        Assert.Equal(1, item.ItemVersion);
        Assert.Null(item.PreviousVersionId);
        Assert.Null(item.DiscriminationIndex);
        Assert.Null(item.CalibrationSampleSize);
        Assert.Equal(AdminReviewStatus.NotRequired, item.ReviewStatus);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Constructor_InvalidDifficultyBand_Throws(int band)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PlacementItemDefinition("speaking", "A1", 1, difficultyBand: band));
    }

    [Fact]
    public void Constructor_NegativeEvidenceWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PlacementItemDefinition("speaking", "A1", 1, evidenceWeight: -1));
    }

    [Fact]
    public void Update_ChangesDifficultyBandAndEvidenceWeight()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1);
        item.Update("speaking", "A1", 1, true, difficultyBand: 3, evidenceWeight: 2.5);

        Assert.Equal(3, item.DifficultyBand);
        Assert.Equal(2.5, item.EvidenceWeight);
    }

    [Fact]
    public void Update_NullCalibrationArgs_PreservesExistingValues()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1, difficultyBand: 4, evidenceWeight: 3.0);
        item.Update("speaking", "A1", 1, true);

        Assert.Equal(4, item.DifficultyBand);
        Assert.Equal(3.0, item.EvidenceWeight);
    }

    [Fact]
    public void SetCalibrationStats_SetsDiscriminationIndexAndSampleSize()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1);
        item.SetCalibrationStats(0.42, 50);

        Assert.Equal(0.42, item.DiscriminationIndex);
        Assert.Equal(50, item.CalibrationSampleSize);
    }

    [Fact]
    public void SetCalibrationStats_NegativeSampleSize_Throws()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => item.SetCalibrationStats(0.5, -1));
    }

    [Fact]
    public void Approve_SetsReviewStatusApproved()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1);
        item.Approve();
        Assert.Equal(AdminReviewStatus.Approved, item.ReviewStatus);
    }

    [Fact]
    public void Reject_SetsReviewStatusRejected_AndDisablesItem()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1, isEnabled: true);
        item.Reject("Not level-appropriate.");

        Assert.Equal(AdminReviewStatus.Rejected, item.ReviewStatus);
        Assert.False(item.IsEnabled);
    }

    [Fact]
    public void Reject_EmptyReason_Throws()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1);
        Assert.Throws<ArgumentException>(() => item.Reject(""));
    }

    [Fact]
    public void ResetToPendingReview_SetsReviewStatusPendingReview()
    {
        var item = new PlacementItemDefinition("speaking", "A1", 1);
        item.Reject("bad");
        item.ResetToPendingReview();
        Assert.Equal(AdminReviewStatus.PendingReview, item.ReviewStatus);
    }
}
