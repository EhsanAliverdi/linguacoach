using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Unit tests for PlacementAssessmentItem entity (Phase 13A).
/// </summary>
public sealed class PlacementAssessmentItemTests
{
    [Fact]
    public void Create_ValidArgs_SetsFields()
    {
        var assessmentId = Guid.NewGuid();
        var item = PlacementAssessmentItem.Create(
            assessmentId, "grammar", "B1", "multiple_choice", "Which is correct?", "A", 0);

        Assert.Equal(assessmentId, item.PlacementAssessmentId);
        Assert.Equal("grammar", item.Skill);
        Assert.Equal("B1", item.TargetCefrLevel);
        Assert.Equal("multiple_choice", item.ItemType);
        Assert.Equal("Which is correct?", item.Prompt);
        Assert.Equal("A", item.CorrectAnswer);
        Assert.Equal(0, item.ItemOrder);
        Assert.Null(item.Response);
        Assert.Null(item.IsCorrect);
    }

    [Fact]
    public void Create_EmptyAssessmentId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlacementAssessmentItem.Create(Guid.Empty, "grammar", "B1", "mc", "Q?", "A", 0));
    }

    [Fact]
    public void Create_EmptySkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlacementAssessmentItem.Create(Guid.NewGuid(), "", "B1", "mc", "Q?", "A", 0));
    }

    [Fact]
    public void Create_EmptyTargetLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlacementAssessmentItem.Create(Guid.NewGuid(), "grammar", "", "mc", "Q?", "A", 0));
    }

    [Fact]
    public void RecordResponse_Correct_SetsFields()
    {
        var item = PlacementAssessmentItem.Create(
            Guid.NewGuid(), "vocabulary", "A2", "multiple_choice", "Q?", "B", 1);

        item.RecordResponse("B", true, 1.0);

        Assert.Equal("B", item.Response);
        Assert.True(item.IsCorrect);
        Assert.Equal(1.0, item.Score);
        Assert.NotNull(item.EvaluatedAtUtc);
    }

    [Fact]
    public void RecordResponse_Incorrect_SetsFields()
    {
        var item = PlacementAssessmentItem.Create(
            Guid.NewGuid(), "reading", "B1", "gap_fill", "Fill: '___'", "concerns", 2);

        item.RecordResponse("wrong", false, 0.0);

        Assert.Equal("wrong", item.Response);
        Assert.False(item.IsCorrect);
        Assert.Equal(0.0, item.Score);
    }

    [Fact]
    public void RecordResponse_WithNotesAndDuration_SetsFields()
    {
        var item = PlacementAssessmentItem.Create(
            Guid.NewGuid(), "grammar", "A2", "multiple_choice", "Q?", "B", 3);

        item.RecordResponse("B", true, 1.0, "Correct. Expected: 'B'.", 12);

        Assert.Equal("Correct. Expected: 'B'.", item.EvaluationNotes);
        Assert.Equal(12, item.DurationSeconds);
    }

    [Fact]
    public void RecordResponse_DuplicateCall_Throws()
    {
        var item = PlacementAssessmentItem.Create(
            Guid.NewGuid(), "grammar", "A1", "multiple_choice", "Q?", "A", 0);
        item.RecordResponse("A", true, 1.0);

        Assert.Throws<InvalidOperationException>(() => item.RecordResponse("A", true, 1.0));
    }
}
