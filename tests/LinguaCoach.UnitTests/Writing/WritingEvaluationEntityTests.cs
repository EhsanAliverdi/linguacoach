using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Writing;

public sealed class WritingEvaluationEntityTests
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

        var eval = WritingEvaluation.CreatePending(attemptId, studentId, activityId);

        eval.ActivityAttemptId.Should().Be(attemptId);
        eval.StudentProfileId.Should().Be(studentId);
        eval.LearningActivityId.Should().Be(activityId);
        eval.Status.Should().Be(WritingEvaluationStatus.Pending);
        eval.RetryCount.Should().Be(0);
        eval.ProviderName.Should().BeNull();
        eval.OverallScore.Should().BeNull();
        eval.FeedbackText.Should().BeNull();
        eval.CorrectedText.Should().BeNull();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void CreatePending_WithEmptyId_Throws(bool emptyAttempt, bool emptyStudent, bool emptyActivity)
    {
        var fn = () => WritingEvaluation.CreatePending(
            emptyAttempt ? Guid.Empty : AttemptId(),
            emptyStudent ? Guid.Empty : StudentId(),
            emptyActivity ? Guid.Empty : ActivityId());

        fn.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkEvaluating_FromPending_SetsStatusAndProvider()
    {
        var eval = WritingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());

        eval.MarkEvaluating("TestProvider", "test-model-v1");

        eval.Status.Should().Be(WritingEvaluationStatus.Evaluating);
        eval.ProviderName.Should().Be("TestProvider");
        eval.ModelName.Should().Be("test-model-v1");
        eval.StartedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkEvaluating_FromCompleted_Throws()
    {
        var eval = WritingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());
        eval.MarkEvaluating("Provider", null);
        eval.MarkCompleted(70, 70, 70, 70, 70, "ok", "improve", null);

        var fn = () => eval.MarkEvaluating("Provider", null);

        fn.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkCompleted_SetsStatusAndScores()
    {
        var eval = WritingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());
        eval.MarkEvaluating("Provider", null);

        eval.MarkCompleted(
            overallScore: 78.5,
            grammarScore: 80.0,
            vocabularyScore: 75.0,
            coherenceScore: 82.0,
            taskCompletionScore: 77.0,
            feedbackText: "Good attempt.",
            suggestedImprovement: "Vary your vocabulary.",
            correctedText: "An improved version.");

        eval.Status.Should().Be(WritingEvaluationStatus.Completed);
        eval.OverallScore.Should().Be(78.5);
        eval.GrammarScore.Should().Be(80.0);
        eval.VocabularyScore.Should().Be(75.0);
        eval.CoherenceScore.Should().Be(82.0);
        eval.TaskCompletionScore.Should().Be(77.0);
        eval.FeedbackText.Should().Be("Good attempt.");
        eval.SuggestedImprovement.Should().Be("Vary your vocabulary.");
        eval.CorrectedText.Should().Be("An improved version.");
        eval.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_AllowsNullScores()
    {
        var eval = WritingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());
        eval.MarkEvaluating("Provider", null);

        eval.MarkCompleted(null, null, null, null, null, null, null, null);

        eval.Status.Should().Be(WritingEvaluationStatus.Completed);
        eval.OverallScore.Should().BeNull();
        eval.CorrectedText.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_IncrementsRetryCount()
    {
        var eval = WritingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());
        eval.MarkEvaluating("Provider", null);

        eval.MarkFailed("API timeout");
        eval.MarkFailed("API timeout again");

        eval.Status.Should().Be(WritingEvaluationStatus.Failed);
        eval.RetryCount.Should().Be(2);
        eval.FailureReason.Should().Be("API timeout again");
        eval.FailedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkNotSupported_SetsCorrectStatus()
    {
        var eval = WritingEvaluation.CreatePending(AttemptId(), StudentId(), ActivityId());

        eval.MarkNotSupported();

        eval.Status.Should().Be(WritingEvaluationStatus.NotSupported);
    }
}
