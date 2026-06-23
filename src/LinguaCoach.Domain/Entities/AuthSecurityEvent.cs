using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Append-only audit trail for authentication and security events.
/// Never stores passwords, tokens, JWTs, or raw Authorization headers.
/// </summary>
public sealed class AuthSecurityEvent : BaseEntity
{
    /// <summary>Resolved user ID when known. Null for events where the user was not found.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    /// Normalized email or username used in the request. Stored for audit traceability.
    /// Never store passwords, tokens, or secrets here.
    /// </summary>
    public string? EmailOrUserName { get; private set; }

    public AuthEventType EventType { get; private set; }
    public AuthEventOutcome Outcome { get; private set; }

    /// <summary>
    /// Code-like failure reason (not a user-facing message).
    /// Examples: InvalidCredentials, LockedOut, PasswordPolicyFailed, InvalidOrExpiredToken.
    /// </summary>
    public string? FailureReasonCode { get; private set; }

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }

    /// <summary>Minimal safe metadata JSON. Must never contain passwords, tokens, or secrets.</summary>
    public string? MetadataJson { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    private AuthSecurityEvent() { }

    public AuthSecurityEvent(
        AuthEventType eventType,
        AuthEventOutcome outcome,
        Guid? userId = null,
        string? emailOrUserName = null,
        string? failureReasonCode = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? correlationId = null,
        string? metadataJson = null)
    {
        EventType = eventType;
        Outcome = outcome;
        UserId = userId;
        EmailOrUserName = emailOrUserName?.ToLowerInvariant().Trim();
        FailureReasonCode = failureReasonCode?.Trim();
        IpAddress = ipAddress?.Trim();
        // Truncate user agent to avoid unbounded string storage
        UserAgent = userAgent is { Length: > 512 } ua ? ua[..512] : userAgent?.Trim();
        CorrelationId = correlationId?.Trim();
        MetadataJson = metadataJson;
        OccurredAtUtc = DateTime.UtcNow;
    }
}
