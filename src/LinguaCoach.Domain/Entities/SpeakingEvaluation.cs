using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Tracks the AI evaluation lifecycle for a submitted audio ActivityAttempt.
/// Created as Pending immediately after audio submission.
/// Status transitions: Pending -> Evaluating -> Completed | Failed | NotSupported.
/// </summary>
public sealed class SpeakingEvaluation : BaseEntity
{
    public Guid ActivityAttemptId { get; private set; }
    public Guid StudentProfileId { get; private set; }
    public Guid LearningActivityId { get; private set; }
    public SpeakingEvaluationStatus Status { get; private set; }
    public string? ProviderName { get; private set; }
    public string? ModelName { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public string? Transcript { get; private set; }
    public double? OverallScore { get; private set; }
    public double? FluencyScore { get; private set; }
    public double? PronunciationScore { get; private set; }
    public double? CompletenessScore { get; private set; }
    public double? RelevanceScore { get; private set; }
    public string? FeedbackText { get; private set; }
    public string? SuggestedImprovement { get; private set; }
    public int RetryCount { get; private set; }

    private SpeakingEvaluation()
    {
        Status = SpeakingEvaluationStatus.Pending;
    }

    public static SpeakingEvaluation CreatePending(
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

        return new SpeakingEvaluation
        {
            ActivityAttemptId = activityAttemptId,
            StudentProfileId = studentProfileId,
            LearningActivityId = learningActivityId,
            Status = SpeakingEvaluationStatus.Pending,
        };
    }

    public void MarkEvaluating(string providerName, string? modelName)
    {
        if (Status != SpeakingEvaluationStatus.Pending && Status != SpeakingEvaluationStatus.Failed)
            throw new InvalidOperationException($"Cannot start evaluation from status {Status}.");
        Status = SpeakingEvaluationStatus.Evaluating;
        ProviderName = providerName?.Trim();
        ModelName = modelName?.Trim();
        StartedAtUtc = DateTime.UtcNow;
    }

    public void MarkCompleted(
        string? transcript,
        double? overallScore,
        double? fluencyScore,
        double? pronunciationScore,
        double? completenessScore,
        double? relevanceScore,
        string? feedbackText,
        string? suggestedImprovement)
    {
        Status = SpeakingEvaluationStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        Transcript = transcript?.Trim();
        OverallScore = overallScore;
        FluencyScore = fluencyScore;
        PronunciationScore = pronunciationScore;
        CompletenessScore = completenessScore;
        RelevanceScore = relevanceScore;
        FeedbackText = feedbackText?.Trim();
        SuggestedImprovement = suggestedImprovement?.Trim();
    }

    public void MarkFailed(string? reason)
    {
        Status = SpeakingEvaluationStatus.Failed;
        FailedAtUtc = DateTime.UtcNow;
        FailureReason = reason?.Trim();
        RetryCount++;
    }

    public void MarkNotSupported()
    {
        Status = SpeakingEvaluationStatus.NotSupported;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
