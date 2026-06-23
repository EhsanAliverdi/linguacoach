using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Represents an active or historical user session via a refresh token.
/// Only the SHA-256 hash of the raw token is stored — the raw token is never persisted.
/// Append-friendly: revocation sets RevokedAtUtc/RevocationReason rather than deleting.
/// </summary>
public sealed class UserRefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 hex hash of the raw refresh token. Never the raw value.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>Id of the replacement token issued during rotation, if any.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public DateTime? LastUsedAtUtc { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceDescription { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? RevocationReason { get; private set; }

    private UserRefreshToken() { }

    public static UserRefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceDescription = null,
        string? correlationId = null)
    {
        return new UserRefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            IpAddress = ipAddress,
            UserAgent = userAgent?.Length > 512 ? userAgent[..512] : userAgent,
            DeviceDescription = deviceDescription,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    public void Revoke(string reason, Guid? replacedById = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        RevocationReason = reason;
        ReplacedByTokenId = replacedById;
    }

    public void RecordUsage()
    {
        LastUsedAtUtc = DateTime.UtcNow;
    }
}
