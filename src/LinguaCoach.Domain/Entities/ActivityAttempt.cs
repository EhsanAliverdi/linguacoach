using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Records one student attempt at a LearningActivity and the AI feedback received.
/// Append-only — never mutated after creation.
/// </summary>
public sealed class ActivityAttempt : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public Guid LearningActivityId { get; private set; }

    // The student's submitted content (text for writing, audio URL for speaking/pronunciation).
    public string SubmittedContent { get; private set; }

    // URL to audio recording if ActivityType involves speaking/pronunciation. Null otherwise.
    public string? AudioUrl { get; private set; }

    // Structured AI feedback stored as JSON. Shape is ActivityType-specific.
    public string FeedbackJson { get; private set; }

    // Numeric score 0–100. Null if AI returned no score or generation failed.
    public double? Score { get; private set; }

    // Which prompt key was used, for auditability and cost tracking.
    public string PromptKey { get; private set; }

    private ActivityAttempt()
    {
        SubmittedContent = string.Empty;
        FeedbackJson = "{}";
        PromptKey = string.Empty;
    }

    public ActivityAttempt(
        Guid studentProfileId,
        Guid learningActivityId,
        string submittedContent,
        string feedbackJson,
        string promptKey,
        double? score = null,
        string? audioUrl = null)
    {
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (learningActivityId == Guid.Empty) throw new ArgumentException("LearningActivityId must not be empty.", nameof(learningActivityId));
        if (string.IsNullOrWhiteSpace(submittedContent)) throw new ArgumentException("SubmittedContent is required.", nameof(submittedContent));
        if (score is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");

        StudentProfileId = studentProfileId;
        LearningActivityId = learningActivityId;
        SubmittedContent = submittedContent.Trim();
        FeedbackJson = string.IsNullOrWhiteSpace(feedbackJson) ? "{}" : feedbackJson;
        PromptKey = promptKey?.Trim() ?? string.Empty;
        Score = score;
        AudioUrl = audioUrl?.Trim();
    }
}
