using Google.Apis.Auth;
using LinguaCoach.Application.Auth;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Auth;

/// <summary>
/// Validates Google ID tokens using the official Google.Apis.Auth library.
/// Token signature and audience are verified against Google's public keys.
/// Raw token is never stored or logged beyond this validation call.
/// </summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(ILogger<GoogleTokenValidator> logger)
    {
        _logger = logger;
    }

    public async Task<GoogleTokenValidationResult> ValidateAsync(
        string idToken,
        string expectedClientId,
        CancellationToken ct = default)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [expectedClientId],
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return GoogleTokenValidationResult.Ok(new GoogleTokenPayload(
                Subject: payload.Subject,
                Email: payload.Email,
                EmailVerified: payload.EmailVerified,
                HostedDomain: payload.HostedDomain,
                DisplayName: payload.Name));
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("Google ID token validation failed: {Reason}", ex.Message);
            return GoogleTokenValidationResult.Fail("InvalidToken");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Google ID token validation.");
            return GoogleTokenValidationResult.Fail("ValidationError");
        }
    }
}
