using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Unit tests for PlacementAssessmentItem entity (Phase 13A; Form.io-native migration).
/// </summary>
public sealed class PlacementAssessmentItemTests
{
    [Fact]
    public void Create_ValidArgs_SetsFields()
    {
        var assessmentId = Guid.NewGuid();
        var item = PlacementAssessmentItem.Create(
            assessmentId, "grammar", "B1", "multiple_choice", "Which is correct?", 0,
            formIoSchemaJson: "{\"components\":[]}", scoringRulesJsonSnapshot: "{\"components\":{}}", scoringRulesVersionSnapshot: 1);

        Assert.Equal(assessmentId, item.PlacementAssessmentId);
        Assert.Equal("grammar", item.Skill);
        Assert.Equal("B1", item.TargetCefrLevel);
        Assert.Equal("multiple_choice", item.ItemType);
        Assert.Equal("Which is correct?", item.Prompt);
        Assert.Equal(0, item.ItemOrder);
        Assert.Equal("{\"components\":[]}", item.FormIoSchemaJson);
        Assert.Equal(1, item.ScoringRulesVersionSnapshot);
        Assert.Null(item.SubmissionDataJson);
        Assert.Null(item.IsCorrect);
    }

    [Fact]
    public void Create_EmptyAssessmentId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlacementAssessmentItem.Create(Guid.Empty, "grammar", "B1", "mc", "Q?", 0));
    }

    [Fact]
    public void Create_EmptySkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlacementAssessmentItem.Create(Guid.NewGuid(), "", "B1", "mc", "Q?", 0));
    }

    [Fact]
    public void Create_EmptyTargetLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlacementAssessmentItem.Create(Guid.NewGuid(), "grammar", "", "mc", "Q?", 0));
    }

    [Fact]
    public void RecordResponse_Correct_SetsFields()
    {
        var item = PlacementAssessmentItem.Create(
            Guid.NewGuid(), "vocabulary", "A2", "multiple_choice", "Q?", 1);

        item.RecordResponse("{\"answer\":\"B\"}", "{\"answer\":\"B\"}", true, 1.0);

        Assert.Equal("{\"answer\":\"B\"}", item.SubmissionDataJson);
        Assert.True(item.IsCorrect);
        Assert.Equal(1.0, item.Score);
        Assert.NotNull(item.EvaluatedAtUtc);
    }

    [Fact]
    public void RecordResponse_Incorrect_SetsFields()
    {
        var item = PlacementAssessmentItem.Create(
            Guid.NewGuid(), "reading", "B1", "gap_fill", "Fill: '___'", 2);

        item.RecordResponse("{\"answer\":\"wrong\"}", "{\"answer\":\"wrong\"}", false, 0.0);

        Assert.Equal("{\"answer\":\"wrong\"}", item.SubmissionDataJson);
        Assert.False(item.IsCorrect);
        Assert.Equal(0.0, item.Score);
    }

    [Fact]
    public void RecordResponse_WithDuration_SetsFields()
    {
        var item = PlacementAssessmentItem.Create(
            Guid.NewGuid(), "grammar", "A2", "multiple_choice", "Q?", 3);

        item.RecordResponse("{\"answer\":\"B\"}", "{\"answer\":\"B\"}", true, 1.0, 12);

        Assert.Equal(12, item.DurationSeconds);
    }

    [Fact]
    public void RecordResponse_DuplicateCall_Throws()
    {
        var item = PlacementAssessmentItem.Create(
            Guid.NewGuid(), "grammar", "A1", "multiple_choice", "Q?", 0);
        item.RecordResponse("{\"answer\":\"A\"}", "{\"answer\":\"A\"}", true, 1.0);

        Assert.Throws<InvalidOperationException>(() =>
            item.RecordResponse("{\"answer\":\"A\"}", "{\"answer\":\"A\"}", true, 1.0));
    }
}
