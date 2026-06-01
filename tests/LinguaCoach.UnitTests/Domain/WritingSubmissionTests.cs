using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class WritingSubmissionTests
{
    private static Guid AStudentId() => Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidArgs_CreatesSubmission()
    {
        var sub = new WritingSubmission(AStudentId(), "Test scenario", "My draft", "Corrected", "{}", 75.0, "writing.exercise.v1");
        sub.ScenarioTitle.Should().Be("Test scenario");
        sub.OriginalText.Should().Be("My draft");
        sub.Score.Should().Be(75.0);
    }

    [Fact]
    public void Constructor_WithEmptyStudentId_Throws()
    {
        var act = () => new WritingSubmission(Guid.Empty, "title", "text", "", "{}", null, "key");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithBlankOriginalText_Throws()
    {
        var act = () => new WritingSubmission(AStudentId(), "title", "  ", "", "{}", null, "key");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithScoreAbove100_Throws()
    {
        var act = () => new WritingSubmission(AStudentId(), "title", "text", "", "{}", 101, "key");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNullFeedbackJson_DefaultsToEmptyObject()
    {
        var sub = new WritingSubmission(AStudentId(), "title", "text", "", null!, null, "key");
        sub.FeedbackJson.Should().Be("{}");
    }

    [Fact]
    public void Constructor_WithNullScore_IsAllowed()
    {
        var sub = new WritingSubmission(AStudentId(), "title", "text", "", "{}", null, "key");
        sub.Score.Should().BeNull();
    }
}
