using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>A student's in-progress or final submission against a StudentFlowTemplateVersion.
/// SubmissionJson is the raw Form.io submission.data payload; NormalizedAnswersJson is the
/// flattened component-key -> value map produced during evaluation.</summary>
public sealed class StudentFlowSubmission : BaseEntity
{
    public Guid StudentId { get; private set; }
    public StudentFlowKind FlowKind { get; private set; }
    public Guid TemplateVersionId { get; private set; }
    public string SubmissionJson { get; private set; } = "{}";
    public string? NormalizedAnswersJson { get; private set; }
    public StudentFlowSubmissionStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? EvaluatedAt { get; private set; }

    private StudentFlowSubmission() { }

    public StudentFlowSubmission(Guid studentId, StudentFlowKind flowKind, Guid templateVersionId, string submissionJson)
    {
        if (studentId == Guid.Empty) throw new ArgumentException("StudentId is required.", nameof(studentId));
        if (templateVersionId == Guid.Empty) throw new ArgumentException("TemplateVersionId is required.", nameof(templateVersionId));

        StudentId = studentId;
        FlowKind = flowKind;
        TemplateVersionId = templateVersionId;
        SubmissionJson = submissionJson;
        Status = StudentFlowSubmissionStatus.Started;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void SaveDraft(string submissionJson)
    {
        if (Status is StudentFlowSubmissionStatus.Submitted or StudentFlowSubmissionStatus.Evaluated)
            throw new InvalidOperationException("Cannot save a draft after final submission.");
        SubmissionJson = submissionJson;
    }

    public void MarkSubmitted(string submissionJson)
    {
        SubmissionJson = submissionJson;
        Status = StudentFlowSubmissionStatus.Submitted;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    public void MarkEvaluated(string normalizedAnswersJson)
    {
        NormalizedAnswersJson = normalizedAnswersJson;
        Status = StudentFlowSubmissionStatus.Evaluated;
        EvaluatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed() => Status = StudentFlowSubmissionStatus.Failed;
}
