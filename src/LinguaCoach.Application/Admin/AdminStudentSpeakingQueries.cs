namespace LinguaCoach.Application.Admin;

public sealed record AdminStudentSpeakingAttemptsQuery(Guid StudentProfileId);

public sealed record AdminStudentSpeakingAttemptDto(
    Guid AttemptId,
    Guid ActivityId,
    string? ActivityTitle,
    string? ActivityType,
    DateTime SubmittedAt,
    string? MimeType,
    /// <summary>Submitted | PendingEvaluation | Evaluated | Failed</summary>
    string Status);

public sealed record AdminStudentSpeakingAttemptsResult(
    /// <summary>Ready | Empty | NotFound</summary>
    string Status,
    IReadOnlyList<AdminStudentSpeakingAttemptDto> Attempts);

public interface IAdminStudentSpeakingAttemptsQuery
{
    Task<AdminStudentSpeakingAttemptsResult> HandleAsync(
        AdminStudentSpeakingAttemptsQuery query, CancellationToken ct = default);
}
