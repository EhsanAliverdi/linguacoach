namespace LinguaCoach.Application.Auth;

public sealed record IssueRefreshTokenCommand(
    Guid UserId,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId);

public sealed record RefreshTokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc);

public sealed record RefreshResult(bool Succeeded, string? AccessToken, string? RefreshToken, DateTime? ExpiresAtUtc, string? Error)
{
    public static RefreshResult Ok(string accessToken, string refreshToken, DateTime expiresAtUtc)
        => new(true, accessToken, refreshToken, expiresAtUtc, null);
    public static RefreshResult Fail(string error)
        => new(false, null, null, null, error);
}

public interface IRefreshTokenService
{
    /// <summary>Issues a new refresh token for an authenticated user. Raw token returned once; only hash stored.</summary>
    Task<RefreshTokenResult> IssueAsync(IssueRefreshTokenCommand command, CancellationToken ct = default);

    /// <summary>
    /// Validates the raw refresh token, rotates it (revokes old, issues new access+refresh tokens).
    /// Reuse of an already-rotated token triggers family revocation.
    /// </summary>
    Task<RefreshResult> RefreshAsync(string rawToken, string? ipAddress, string? userAgent, string? correlationId, CancellationToken ct = default);

    /// <summary>Revokes a single refresh token by raw value. No-op if not found.</summary>
    Task RevokeAsync(string rawToken, string reason, CancellationToken ct = default);

    /// <summary>Revokes all active refresh tokens for a user.</summary>
    Task RevokeAllAsync(Guid userId, string reason, CancellationToken ct = default);
}
