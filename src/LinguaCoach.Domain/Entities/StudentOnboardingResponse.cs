using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

public sealed class StudentOnboardingResponse : BaseEntity
{
    public Guid ProgressId { get; private set; }
    public StudentOnboardingProgress? Progress { get; private set; }

    public string StepKey { get; private set; } = string.Empty;
    public string AnswerJson { get; private set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; private set; }

    private StudentOnboardingResponse() { }

    public StudentOnboardingResponse(Guid progressId, string stepKey, string answerJson)
    {
        if (progressId == Guid.Empty) throw new ArgumentException("ProgressId required.", nameof(progressId));
        if (string.IsNullOrWhiteSpace(stepKey)) throw new ArgumentException("StepKey required.", nameof(stepKey));
        if (string.IsNullOrWhiteSpace(answerJson)) throw new ArgumentException("AnswerJson required.", nameof(answerJson));

        ProgressId = progressId;
        StepKey = stepKey;
        AnswerJson = answerJson;
        SubmittedAt = DateTimeOffset.UtcNow;
    }
}
