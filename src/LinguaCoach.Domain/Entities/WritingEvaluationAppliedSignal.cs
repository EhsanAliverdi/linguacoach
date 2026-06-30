using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Immutable audit record for a writing evaluation signal that was applied to student learning state.
/// Written once per evaluation. Guarantees idempotency — one record per EvaluationId.
/// Source: WritingEvaluation. Applied rule version: 17C-v1.
/// CEFR updates, objective completion, and Learning Plan regeneration are permanently off.
/// </summary>
public sealed class WritingEvaluationAppliedSignal : BaseEntity
{
    public Guid EvaluationId { get; private set; }
    public Guid AttemptId { get; private set; }
    public Guid StudentProfileId { get; private set; }
    public Guid ActivityId { get; private set; }

    // "Review" or "Positive"
    public string SignalType { get; private set; } = string.Empty;

    // "Low" | "Medium" | "High"
    public string Confidence { get; private set; } = string.Empty;

    public double? ScoreUsed { get; private set; }

    // Always "writing" in Phase 17C
    public string SkillAffected { get; private set; } = string.Empty;

    // Rule version for auditability — "17C-v1"
    public string AppliedRuleVersion { get; private set; } = string.Empty;

    // The dry-run outcome that led to this application e.g. "CandidateReviewSignal"
    public string DryRunOutcome { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    // Null when application did not produce a StudentLearningEvent (future use)
    public Guid? LearningEventId { get; private set; }

    public DateTime AppliedAtUtc { get; private set; }

    private WritingEvaluationAppliedSignal() { }

    public static WritingEvaluationAppliedSignal Create(
        Guid evaluationId,
        Guid attemptId,
        Guid studentProfileId,
        Guid activityId,
        string signalType,
        string confidence,
        double? scoreUsed,
        string skillAffected,
        string dryRunOutcome,
        string reason,
        Guid? learningEventId = null)
    {
        if (evaluationId == Guid.Empty) throw new ArgumentException("EvaluationId must not be empty.", nameof(evaluationId));
        if (attemptId == Guid.Empty) throw new ArgumentException("AttemptId must not be empty.", nameof(attemptId));
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(signalType)) throw new ArgumentException("SignalType is required.", nameof(signalType));

        return new WritingEvaluationAppliedSignal
        {
            EvaluationId = evaluationId,
            AttemptId = attemptId,
            StudentProfileId = studentProfileId,
            ActivityId = activityId,
            SignalType = signalType.Trim(),
            Confidence = confidence?.Trim() ?? string.Empty,
            ScoreUsed = scoreUsed,
            SkillAffected = skillAffected?.Trim() ?? "writing",
            AppliedRuleVersion = "17C-v1",
            DryRunOutcome = dryRunOutcome?.Trim() ?? string.Empty,
            Reason = reason?.Trim() ?? string.Empty,
            LearningEventId = learningEventId,
            AppliedAtUtc = DateTime.UtcNow,
        };
    }
}
