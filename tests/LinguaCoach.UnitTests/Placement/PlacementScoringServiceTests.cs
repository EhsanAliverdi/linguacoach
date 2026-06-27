using LinguaCoach.Infrastructure.Placement;

namespace LinguaCoach.UnitTests.Placement;

/// <summary>
/// Unit tests for PlacementScoringService (Phase 13B).
/// </summary>
public sealed class PlacementScoringServiceTests
{
    private readonly PlacementScoringService _sut = new();

    [Fact]
    public void Score_ExactMatch_ReturnsCorrect()
    {
        var result = _sut.Score("A", "A", "multiple_choice");
        Assert.True(result.IsCorrect);
        Assert.Equal(1.0, result.Score);
        Assert.Contains("Correct", result.EvaluationNotes);
    }

    [Fact]
    public void Score_CaseInsensitiveMatch_ReturnsCorrect()
    {
        var result = _sut.Score("best", "Best", "gap_fill");
        Assert.True(result.IsCorrect);
        Assert.Equal(1.0, result.Score);
    }

    [Fact]
    public void Score_WhitespaceNormalized_ReturnsCorrect()
    {
        var result = _sut.Score("  B  ", "B", "multiple_choice");
        Assert.True(result.IsCorrect);
    }

    [Fact]
    public void Score_WrongAnswer_ReturnsIncorrect()
    {
        var result = _sut.Score("C", "A", "multiple_choice");
        Assert.False(result.IsCorrect);
        Assert.Equal(0.0, result.Score);
        Assert.Contains("Incorrect", result.EvaluationNotes);
        Assert.Contains("Expected: 'A'", result.EvaluationNotes);
        Assert.Contains("Received: 'C'", result.EvaluationNotes);
    }

    [Fact]
    public void Score_EmptyResponse_ReturnsIncorrect()
    {
        var result = _sut.Score("", "A", "multiple_choice");
        Assert.False(result.IsCorrect);
        Assert.Equal(0.0, result.Score);
        Assert.Contains("Empty response", result.EvaluationNotes);
    }

    [Fact]
    public void Score_WhitespaceOnlyResponse_ReturnsIncorrect()
    {
        var result = _sut.Score("   ", "A", "multiple_choice");
        Assert.False(result.IsCorrect);
    }

    [Fact]
    public void Score_NullCorrectAnswer_ReturnsIncorrect()
    {
        var result = _sut.Score("A", null, "multiple_choice");
        Assert.False(result.IsCorrect);
        Assert.Contains("No correct answer", result.EvaluationNotes);
    }

    [Fact]
    public void Score_GapFill_CorrectAnswer_ReturnsCorrect()
    {
        var result = _sut.Score("are", "are", "gap_fill");
        Assert.True(result.IsCorrect);
    }

    [Fact]
    public void Score_MultiWord_ExactMatch_ReturnsCorrect()
    {
        var result = _sut.Score("build on", "build on", "gap_fill");
        Assert.True(result.IsCorrect);
    }
}
