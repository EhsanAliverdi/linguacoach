using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Tracks the AI evaluation lifecycle for a submitted written ActivityAttempt.
/// Created as Pending immediately after a WritingScenario submission.
/// Status transitions: Pending -> Evaluating -> Completed | Failed | NotSupported.
/// Phase 17A — foundation only. Never updates mastery, CEFR, objectives, or the Learning Plan.
/// </summary>
public sealed class WritingEvaluation : BaseEntity
{
    public Guid ActivityAttemptId { get; private set; }
    public Guid StudentProfileId { get; private set; }
    public Guid LearningActivityId { get; private set; }
    public WritingEvaluationStatus Status { get; private set; }
    public string? ProviderName { get; private set; }
    public string? ModelName { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public double? OverallScore { get; private set; }
    public double? GrammarScore { get; private set; }
    public double? VocabularyScore { get; private set; }
    public double? CoherenceScore { get; private set; }
    public double? TaskCompletionScore { get; private set; }
    public string? FeedbackText { get; private set; }
    public string? SuggestedImprovement { get; private set; }
    public string? CorrectedText { get; private set; }
    public int RetryCount { get; private set; }

    private WritingEvaluation()
    {
        Status = WritingEvaluationStatus.Pending;
    }

    public static WritingEvaluation CreatePending(
        Guid activityAttemptId,
        Guid studentProfileId,
        Guid learningActivityId)
    {
        if (activityAttemptId == Guid.Empty)
            throw new ArgumentException("ActivityAttemptId must not be empty.", nameof(activityAttemptId));
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (learningActivityId == Guid.Empty)
            throw new ArgumentException("LearningActivityId must not be empty.", nameof(learningActivityId));

        return new WritingEvaluation
        {
            ActivityAttemptId = activityAttemptId,
            StudentProfileId = studentProfileId,
            LearningActivityId = learningActivityId,
            Status = WritingEvaluationStatus.Pending,
        };
    }

    public void MarkEvaluating(string providerName, string? modelName)
    {
        if (Status != WritingEvaluationStatus.Pending && Status != WritingEvaluationStatus.Failed)
            throw new InvalidOperationException($"Cannot start evaluation from status {Status}.");
        Status = WritingEvaluationStatus.Evaluating;
        ProviderName = providerName?.Trim();
        ModelName = modelName?.Trim();
        StartedAtUtc = DateTime.UtcNow;
    }

    public void MarkCompleted(
        double? overallScore,
        double? grammarScore,
        double? vocabularyScore,
        double? coherenceScore,
        double? taskCompletionScore,
        string? feedbackText,
        string? suggestedImprovement,
        string? correctedText)
    {
        Status = WritingEvaluationStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        OverallScore = overallScore;
        GrammarScore = grammarScore;
        VocabularyScore = vocabularyScore;
        CoherenceScore = coherenceScore;
        TaskCompletionScore = taskCompletionScore;
        FeedbackText = feedbackText?.Trim();
        SuggestedImprovement = suggestedImprovement?.Trim();
        CorrectedText = correctedText?.Trim();
    }

    public void MarkFailed(string? reason)
    {
        Status = WritingEvaluationStatus.Failed;
        FailedAtUtc = DateTime.UtcNow;
        FailureReason = reason?.Trim();
        RetryCount++;
    }

    public void MarkNotSupported()
    {
        Status = WritingEvaluationStatus.NotSupported;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
