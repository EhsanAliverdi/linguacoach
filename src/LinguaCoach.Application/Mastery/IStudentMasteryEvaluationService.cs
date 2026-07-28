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

    /// <summary>Skill Graph container/leaf Phase 3 (2026-07-27) — rolls up a container node's
    /// display status from its Approved+Active leaf children's real per-node mastery (percent of
    /// leaves currently <see cref="Domain.Enums.MasteryStatus.Mastered"/>, banded ≥80% → Mastered,
    /// same threshold convention <see cref="EvaluateObjectiveMasteryAsync"/>'s own scoring already
    /// uses elsewhere). A container is a display/admin-review concept, never a delivery target
    /// itself — this never feeds routing/selection, only dashboards. A container with zero eligible
    /// leaf children returns <see cref="Domain.Enums.MasteryStatus.InsufficientEvidence"/>.</summary>
    Task<ContainerMasteryRollup> EvaluateContainerRollupAsync(
        Guid studentId,
        Guid containerNodeId,
        CancellationToken ct = default);
}
