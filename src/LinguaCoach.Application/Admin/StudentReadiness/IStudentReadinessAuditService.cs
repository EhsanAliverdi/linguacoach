namespace LinguaCoach.Application.Admin.StudentReadiness;

/// <summary>
/// Read-only "can this student safely use the app end-to-end today?" audit. Never mutates any
/// data — every check is a targeted query or a call to an already-safe, non-mutating service
/// method.
/// </summary>
public interface IStudentReadinessAuditService
{
    /// <summary>Returns null if no StudentProfile exists for the given id.</summary>
    Task<StudentReadinessSummaryDto?> GetReadinessAsync(Guid studentProfileId, CancellationToken ct = default);
}
