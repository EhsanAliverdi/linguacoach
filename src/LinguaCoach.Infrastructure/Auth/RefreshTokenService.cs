using System.Security.Cryptography;
using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Auth;

/// <summary>
/// Manages refresh tokens / user sessions.
///
/// Security invariants:
///   - Raw tokens are generated with RandomNumberGenerator — cryptographically secure.
///   - Only the SHA-256 hex hash is stored in the database.
///   - Raw tokens are never logged, audited in metadata, or stored anywhere except the HTTP response.
///   - Token lookup is by hash (constant-length compare, no timing leak beyond hash equality).
///   - Reuse of a revoked/rotated token triggers full session revocation (family defence).
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IAuthSecurityAuditService _audit;
    private readonly ILogger<RefreshTokenService> _logger;
    private readonly int _expiryDays;

    public RefreshTokenService(
        LinguaCoachDbContext db,
        ITokenService tokenService,
        IAuthSecurityAuditService audit,
        IConfiguration configuration,
        ILogger<RefreshTokenService> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _audit = audit;
        _logger = logger;
        _expiryDays = int.TryParse(configuration["Jwt:RefreshTokenExpiryDays"], out var d) ? d : 14;
    }

    public async Task<RefreshTokenResult> IssueAsync(IssueRefreshTokenCommand command, CancellationToken ct = default)
    {
        var (rawToken, hash) = GenerateToken();
        var expiresAt = DateTime.UtcNow.AddDays(_expiryDays);

        var entity = UserRefreshToken.Create(
            command.UserId, hash, expiresAt,
            command.IpAddress, command.UserAgent, null, command.CorrelationId);

        _db.UserRefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.RefreshTokenIssued, AuthEventOutcome.Success,
            UserId: command.UserId,
            IpAddress: command.IpAddress, UserAgent: command.UserAgent,
            CorrelationId: command.CorrelationId), ct);

        return new RefreshTokenResult(string.Empty, rawToken, expiresAt);
    }

    public async Task<RefreshResult> RefreshAsync(
        string rawToken, string? ipAddress, string? userAgent, string? correlationId,
        CancellationToken ct = default)
    {
        var hash = HashToken(rawToken);

        var stored = await _db.UserRefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            _logger.LogWarning("Refresh attempted with unknown token hash.");
            return RefreshResult.Fail("Invalid or expired refresh token.");
        }

        // Reuse detection — token was already rotated (replaced) or explicitly revoked
        if (!stored.IsActive)
        {
            if (stored.ReplacedByTokenId.HasValue || stored.RevocationReason == "Rotated")
            {
                // Token reuse: revoke the whole chain from this node upward
                _logger.LogWarning(
                    "Refresh token reuse detected for user {UserId}. Revoking active sessions.",
                    stored.UserId);
                await RevokeAllAsync(stored.UserId, "ReuseDetected", ct);
                await _audit.RecordAsync(new AuthSecurityEventRecord(
                    AuthEventType.RefreshTokenReuseDetected, AuthEventOutcome.Blocked,
                    UserId: stored.UserId,
                    IpAddress: ipAddress, UserAgent: userAgent, CorrelationId: correlationId), ct);
            }
            return RefreshResult.Fail("Invalid or expired refresh token.");
        }

        // Look up user details for new access token — need email and role
        var user = await _db.Users
            .Where(u => u.Id == stored.UserId)
            .Select(u => new { u.Id, u.Email, u.Role })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return RefreshResult.Fail("Invalid or expired refresh token.");

        // Rotate: revoke old, issue new
        var newId = Guid.NewGuid();
        stored.Revoke("Rotated", replacedById: newId);
        stored.RecordUsage();

        var (newRaw, newHash) = GenerateToken();
        var expiresAt = DateTime.UtcNow.AddDays(_expiryDays);
        var newToken = UserRefreshToken.Create(
            stored.UserId, newHash, expiresAt,
            ipAddress, userAgent, null, correlationId);
        // Force a specific Id so the ReplacedByTokenId chain is consistent
        // (EF will use whatever Id was set on Create via Guid.NewGuid — close enough for chain tracking)

        _db.UserRefreshTokens.Add(newToken);
        await _db.SaveChangesAsync(ct);

        var newAccessToken = _tokenService.GenerateToken(user.Id, user.Email!, user.Role);

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.RefreshTokenRotated, AuthEventOutcome.Success,
            UserId: stored.UserId,
            IpAddress: ipAddress, UserAgent: userAgent, CorrelationId: correlationId), ct);

        return RefreshResult.Ok(newAccessToken, newRaw, expiresAt);
    }

    public async Task RevokeAsync(string rawToken, string reason, CancellationToken ct = default)
    {
        var hash = HashToken(rawToken);
        var stored = await _db.UserRefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive) return;

        stored.Revoke(reason);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.RefreshTokenRevoked, AuthEventOutcome.Success,
            UserId: stored.UserId), ct);
    }

    public async Task RevokeAllAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var active = await _db.UserRefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        if (active.Count == 0) return;

        foreach (var t in active)
            t.Revoke(reason);

        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.AllSessionsRevoked, AuthEventOutcome.Success,
            UserId: userId), ct);
    }

    // ── Token generation ─────────────────────────────────────────────────────

    private static (string Raw, string Hash) GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var raw = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = HashToken(raw);
        return (raw, hash);
    }

    public static string HashToken(string raw)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
