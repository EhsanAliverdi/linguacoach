using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Admin;

/// <summary>
/// Builds the admin security settings read model from live configuration.
/// Secrets (JWT signing key, Google ClientSecret) are never included.
/// Only presence (configured yes/no) is returned for secrets.
/// </summary>
public sealed class AdminSecurityHandler : IAdminSecurityHandler
{
    private readonly IConfiguration _configuration;
    private readonly IdentityOptions _identityOptions;
    private readonly GoogleExternalLoginOptions _googleOptions;

    public AdminSecurityHandler(
        IConfiguration configuration,
        IOptions<IdentityOptions> identityOptions,
        IOptions<GoogleExternalLoginOptions> googleOptions)
    {
        _configuration = configuration;
        _identityOptions = identityOptions.Value;
        _googleOptions = googleOptions.Value;
    }

    public Task<AdminSecuritySettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = new AdminSecuritySettings(
            PasswordPolicy: BuildPasswordPolicy(),
            Lockout: BuildLockout(),
            RateLimitPolicies: BuildRateLimitPolicies(),
            Jwt: BuildJwt(),
            RefreshToken: BuildRefreshToken(),
            SecurityHeaders: BuildSecurityHeaders(),
            ExternalLogin: BuildExternalLogin());

        return Task.FromResult(settings);
    }

    private AdminPasswordPolicySettings BuildPasswordPolicy()
    {
        var pw = _identityOptions.Password;
        return new AdminPasswordPolicySettings(
            RequiredLength: pw.RequiredLength,
            RequireUppercase: pw.RequireUppercase,
            RequireLowercase: pw.RequireLowercase,
            RequireDigit: pw.RequireDigit,
            RequireNonAlphanumeric: pw.RequireNonAlphanumeric);
    }

    private AdminLockoutSettings BuildLockout()
    {
        var lo = _identityOptions.Lockout;
        return new AdminLockoutSettings(
            MaxFailedAccessAttempts: lo.MaxFailedAccessAttempts,
            LockoutDurationMinutes: (int)lo.DefaultLockoutTimeSpan.TotalMinutes);
    }

    private static IReadOnlyList<AdminRateLimitPolicyInfo> BuildRateLimitPolicies() =>
    [
        new("AuthLogin", PermitLimit: 10, WindowMinutes: 5, KeyedBy: "IP"),
        new("AuthReset", PermitLimit: 3, WindowMinutes: 15, KeyedBy: "IP"),
        new("AuthChangePassword", PermitLimit: 10, WindowMinutes: 5, KeyedBy: "UserId"),
        new("AuthRefresh", PermitLimit: 30, WindowMinutes: 5, KeyedBy: "IP"),
        new("AuthExternalLogin", PermitLimit: 20, WindowMinutes: 5, KeyedBy: "IP"),
    ];

    private AdminJwtSettings BuildJwt()
    {
        var expiryHours = int.TryParse(_configuration["Jwt:ExpiryHours"], out var h) ? h : 24;
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        return new AdminJwtSettings(
            AccessTokenExpiryHours: expiryHours,
            IssuerConfigured: !string.IsNullOrWhiteSpace(issuer),
            AudienceConfigured: !string.IsNullOrWhiteSpace(audience));
    }

    private AdminRefreshTokenSettings BuildRefreshToken()
    {
        var expiryDays = int.TryParse(_configuration["Jwt:RefreshTokenExpiryDays"], out var d) ? d : 14;
        return new AdminRefreshTokenSettings(
            ExpiryDays: expiryDays,
            RotationEnabled: true,
            RevokeOnPasswordChange: true,
            RevokeOnPasswordReset: true);
    }

    private static AdminSecurityHeadersSettings BuildSecurityHeaders() =>
        new(
            XContentTypeOptionsEnabled: true,
            XFrameOptionsEnabled: true,
            ReferrerPolicyEnabled: true,
            PermissionsPolicyEnabled: true,
            CspStatus: "Deferred — Angular nonce strategy required",
            HstsStatus: "Deferred — production TLS confirmation required");

    private AdminGoogleExternalLoginSettings BuildGoogleSettings() =>
        new(
            Enabled: _googleOptions.Enabled,
            ClientIdConfigured: !string.IsNullOrWhiteSpace(_googleOptions.ClientId),
            ClientSecretConfigured: !string.IsNullOrWhiteSpace(_googleOptions.ClientSecret),
            AllowAutoLinkByEmail: _googleOptions.AllowAutoLinkByEmail,
            AllowStudentAutoProvisioning: _googleOptions.AllowStudentAutoProvisioning,
            AllowedDomains: _googleOptions.AllowedDomains.AsReadOnly());

    private AdminExternalLoginSettings BuildExternalLogin() =>
        new(Google: BuildGoogleSettings());
}
