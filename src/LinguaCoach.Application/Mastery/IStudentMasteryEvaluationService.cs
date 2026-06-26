using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Mastery;

/// <summary>
/// Evaluates mastery for students based on their learning event history.
/// Deterministic and side-effect-free (read path) except for EvaluateAndDemoteReadinessItemsAsync
/// which writes demotion state back to the readiness pool.
/// No AI calls — all rules are rule-based thresholds.
/// </summary>
public interface IStudentMasteryEvaluationService
{
    /// <summary>
    /// Evaluates mastery across all skills/objectives the student has touched.
    /// Also calls EvaluateAndDemoteReadinessItemsAsync internally.
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

    /// <summary>
    /// Determines what demotion action (if any) should be applied to a single readiness item.
    /// Does NOT mutate the item — call EvaluateAndDemoteReadinessItemsAsync to apply.
    /// </summary>
    Task<ReadinessDemotionDecision> EvaluateReadinessItemFitAsync(
        Guid studentId,
        Guid readinessItemId,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates all active readiness pool items for the student and applies demotion decisions.
    /// Calls RecordEvaluation() on each item and saves. Returns the total number of items changed.
    /// </summary>
    Task<int> EvaluateAndDemoteReadinessItemsAsync(
        Guid studentId,
        CancellationToken ct = default);
}
