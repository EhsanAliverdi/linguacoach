using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Auth;

public sealed record ExternalLoginRequest(
    string IdToken,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId);

public sealed record ExternalLoginResult(
    bool Succeeded,
    string? AccessToken,
    string? RefreshToken,
    DateTime? RefreshExpiresAtUtc,
    UserRole? Role,
    bool MustChangePassword,
    string? Error)
{
    public static ExternalLoginResult Ok(
        string accessToken,
        string refreshToken,
        DateTime refreshExpiresAtUtc,
        UserRole role,
        bool mustChangePassword)
        => new(true, accessToken, refreshToken, refreshExpiresAtUtc, role, mustChangePassword, null);

    public static ExternalLoginResult Fail(string error)
        => new(false, null, null, null, null, false, error);
}

public interface IExternalLoginService
{
    Task<ExternalLoginResult> GoogleLoginAsync(ExternalLoginRequest request, CancellationToken ct = default);
}
