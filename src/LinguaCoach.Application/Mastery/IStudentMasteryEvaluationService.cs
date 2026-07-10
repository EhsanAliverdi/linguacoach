namespace LinguaCoach.Application.Mastery;

/// <summary>
/// Evaluates mastery for students based on their learning event history.
/// Deterministic and side-effect-free — all rules are rule-based thresholds, no AI calls.
/// Phase I2C: the readiness-pool demotion side effect (EvaluateReadinessItemFitAsync /
/// EvaluateAndDemoteReadinessItemsAsync) was removed along with StudentActivityReadinessItem —
/// see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md. This service is now
/// purely a read path.
/// </summary>
public interface IStudentMasteryEvaluationService
{
    /// <summary>
    /// Evaluates mastery across all skills/objectives the student has touched.
    /// </summary>
    Task<StudentMasteryReport> EvaluateStudentAsync(
        Guid studentId,
        MasteryEvaluationReason reason,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates mastery for a single curriculum objective key.
    /// Uses PrimarySkill of matching events as skillKey.
    /// </summary>
    Task<ObjectiveMasterySignal> EvaluateObjectiveMasteryAsync(
        Guid studentId,
        string objectiveKey,
        CancellationToken ct = default);
}
