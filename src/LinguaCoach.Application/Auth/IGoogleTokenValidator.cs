namespace LinguaCoach.Application.Auth;

/// <summary>
/// Validated payload extracted from a Google ID token.
/// Raw token is never stored beyond validation.
/// </summary>
public sealed record GoogleTokenPayload(
    string Subject,
    string Email,
    bool EmailVerified,
    string? HostedDomain,
    string? DisplayName);

public sealed record GoogleTokenValidationResult(bool IsValid, GoogleTokenPayload? Payload, string? Error)
{
    public static GoogleTokenValidationResult Ok(GoogleTokenPayload payload) => new(true, payload, null);
    public static GoogleTokenValidationResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Validates a Google ID token and extracts its claims.
/// Abstracted so tests can inject a fake without calling real Google APIs.
/// </summary>
public interface IGoogleTokenValidator
{
    Task<GoogleTokenValidationResult> ValidateAsync(string idToken, string expectedClientId, CancellationToken ct = default);
}
