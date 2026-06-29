using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class SpeakingEvaluationTests
{
    private static Guid AttemptId() => Guid.NewGuid();
    private static Guid StudentId() => Guid.NewGuid();
    private static Guid ActivityId() => Guid.NewGuid();

    [Fact]
    public void CreatePending_WithValidIds_ReturnsPendingEvaluation()
    {
        var attemptId = AttemptId();
        var studentId = StudentId();
        var activityId = ActivityId();

        var eval = SpeakingEvaluation.CreatePending(attemptId, studentId, activityId);

        eval.ActivityAttemptId.Should().Be(attemptId);
        eval.StudentProfileId.Should().Be(studentId);
        eval.LearningActivityId.Should().Be(activityId);
        eval.Status.Should().Be(SpeakingEvaluationStatus.Pending);
        eval.RetryCount.Should().Be(0);
        eval.ProviderName.Should().BeNull();
        eval.Transcript.Should().BeNull();
        eval.OverallScore.Should().BeNull();
        eval.FeedbackText.Should().BeNull();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void CreatePending_WithEmptyId_Throws(bool emptyAttempt, bool emptyStudent, bool emptyActivity)
    {
        var fn = () => SpeakingEvaluation.CreatePending(
            emptyAttempt ? Guid.Empty : AttemptId(),
            emptyStudent ? Guid.Empty : StudentId(),
            emptyActivity ? Guid.Empty : ActivityId());

        fn.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkEvaluating_FromPending_SetsStatusAndProvider()
    {
        var eval = SpeakingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());

        eval.MarkEvaluating("TestProvider", "test-model-v1");

        eval.Status.Should().Be(SpeakingEvaluationStatus.Evaluating);
        eval.ProviderName.Should().Be("TestProvider");
        eval.ModelName.Should().Be("test-model-v1");
        eval.StartedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_SetsStatusAndScores()
    {
        var eval = SpeakingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());
        eval.MarkEvaluating("Provider", null);

        eval.MarkCompleted(
            transcript: "Hello world",
            overallScore: 78.5,
            fluencyScore: 80.0,
            pronunciationScore: 75.0,
            completenessScore: 82.0,
            relevanceScore: 77.0,
            feedbackText: "Good attempt.",
            suggestedImprovement: "Work on fluency.");

        eval.Status.Should().Be(SpeakingEvaluationStatus.Completed);
        eval.Transcript.Should().Be("Hello world");
        eval.OverallScore.Should().Be(78.5);
        eval.FluencyScore.Should().Be(80.0);
        eval.FeedbackText.Should().Be("Good attempt.");
        eval.SuggestedImprovement.Should().Be("Work on fluency.");
        eval.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_IncrementsRetryCount()
    {
        var eval = SpeakingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());
        eval.MarkEvaluating("Provider", null);

        eval.MarkFailed("API timeout");
        eval.MarkFailed("API timeout again");

        eval.Status.Should().Be(SpeakingEvaluationStatus.Failed);
        eval.RetryCount.Should().Be(2);
        eval.FailureReason.Should().Be("API timeout again");
        eval.FailedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkNotSupported_SetsCorrectStatus()
    {
        var eval = SpeakingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());

        eval.MarkNotSupported();

        eval.Status.Should().Be(SpeakingEvaluationStatus.NotSupported);
    }

    [Fact]
    public void StatusTransitions_DoNotClearRetryCount()
    {
        var eval = SpeakingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());
        eval.MarkEvaluating("Provider", null);
        eval.MarkFailed("error");

        eval.RetryCount.Should().Be(1);

        eval.MarkEvaluating("Provider", null);
        eval.MarkFailed("error again");

        eval.RetryCount.Should().Be(2);
    }
}
