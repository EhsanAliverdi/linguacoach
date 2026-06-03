using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class ActivityAttemptTests
{
    private static Guid StudentId() => Guid.NewGuid();
    private static Guid ActivityId() => Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidArgs_CreatesAttempt()
    {
        var attempt = new ActivityAttempt(
            StudentId(), ActivityId(), "Dear Manager, I am following up...",
            """{"overallScore":80}""", "activity_evaluate_writing", score: 80.0);

        attempt.SubmittedContent.Should().StartWith("Dear Manager");
        attempt.Score.Should().Be(80.0);
        attempt.AudioUrl.Should().BeNull();
        attempt.PromptKey.Should().Be("activity_evaluate_writing");
    }

    [Fact]
    public void Constructor_WithEmptyStudentId_Throws()
    {
        var fn = () => new ActivityAttempt(Guid.Empty, ActivityId(), "content", "{}", "key");
        fn.Should().Throw<ArgumentException>().WithMessage("*StudentProfileId*");
    }

    [Fact]
    public void Constructor_WithEmptyActivityId_Throws()
    {
        var fn = () => new ActivityAttempt(StudentId(), Guid.Empty, "content", "{}", "key");
        fn.Should().Throw<ArgumentException>().WithMessage("*LearningActivityId*");
    }

    [Fact]
    public void Constructor_WithBlankSubmittedContent_Throws()
    {
        var fn = () => new ActivityAttempt(StudentId(), ActivityId(), "   ", "{}", "key");
        fn.Should().Throw<ArgumentException>().WithMessage("*SubmittedContent*");
    }

    [Fact]
    public void Constructor_WithScoreAbove100_Throws()
    {
        var fn = () => new ActivityAttempt(StudentId(), ActivityId(), "content", "{}", "key", score: 101);
        fn.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Score*");
    }

    [Fact]
    public void Constructor_WithScoreBelow0_Throws()
    {
        var fn = () => new ActivityAttempt(StudentId(), ActivityId(), "content", "{}", "key", score: -1);
        fn.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Score*");
    }

    [Fact]
    public void Constructor_WithNullScore_IsAllowed()
    {
        var attempt = new ActivityAttempt(StudentId(), ActivityId(), "content", "{}", "key", score: null);
        attempt.Score.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAudioUrl_SetsAudioUrl()
    {
        var attempt = new ActivityAttempt(
            StudentId(), ActivityId(), "content", "{}", "key",
            audioUrl: "https://storage.example.com/audio/abc.webm");
        attempt.AudioUrl.Should().Be("https://storage.example.com/audio/abc.webm");
    }

    [Fact]
    public void Constructor_WithNullFeedbackJson_DefaultsToEmptyObject()
    {
        var attempt = new ActivityAttempt(StudentId(), ActivityId(), "content", null!, "key");
        attempt.FeedbackJson.Should().Be("{}");
    }
}
