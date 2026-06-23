using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Auth;

/// <summary>
/// Records authentication and security audit events.
/// Implementations must not throw back into auth flows on persistence failure.
/// </summary>
public interface IAuthSecurityAuditService
{
    Task RecordAsync(AuthSecurityEventRecord record, CancellationToken ct = default);
}

/// <summary>
/// Carries all fields for a single auth/security audit event.
/// Callers must never populate EmailOrUserName, FailureReasonCode, or MetadataJson
/// with passwords, tokens, JWTs, or any other secret.
/// </summary>
public sealed record AuthSecurityEventRecord(
    AuthEventType EventType,
    AuthEventOutcome Outcome,
    Guid? UserId = null,
    string? EmailOrUserName = null,
    string? FailureReasonCode = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CorrelationId = null,
    string? MetadataJson = null);
