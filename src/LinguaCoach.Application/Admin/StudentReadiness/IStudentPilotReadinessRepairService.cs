namespace LinguaCoach.Application.Admin.StudentReadiness;

/// <summary>
/// Explicit, admin-triggered repair/backfill actions for one student's pilot readiness.
/// Every real (non-dry-run) call requires a reason and writes one AdminAuditLog row. Repairs
/// are idempotent and never delete attempts/submissions/evaluations.
/// </summary>
public interface IStudentPilotReadinessRepairService
{
    /// <exception cref="KeyNotFoundException">Unknown student or unknown action key.</exception>
    /// <exception cref="InvalidOperationException">Action is not implemented.</exception>
    /// <exception cref="ArgumentException">Reason missing on a non-dry-run request.</exception>
    Task<StudentReadinessRepairResultDto> RepairAsync(
        Guid studentProfileId,
        Guid adminUserId,
        StudentReadinessRepairRequestDto request,
        CancellationToken ct = default);

    /// <exception cref="KeyNotFoundException">Unknown student.</exception>
    /// <exception cref="ArgumentException">Reason missing on a non-dry-run request.</exception>
    Task<IReadOnlyList<StudentReadinessRepairResultDto>> RunAllSafeRepairsAsync(
        Guid studentProfileId,
        Guid adminUserId,
        string? reason,
        bool dryRun,
        CancellationToken ct = default);
}
