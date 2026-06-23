namespace LinguaCoach.Application.Admin;

// ── Read model ────────────────────────────────────────────────────────────────

public sealed record AdminPasswordPolicySettings(
    int RequiredLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireDigit,
    bool RequireNonAlphanumeric);

public sealed record AdminLockoutSettings(
    int MaxFailedAccessAttempts,
    int LockoutDurationMinutes);

public sealed record AdminRateLimitPolicyInfo(
    string PolicyName,
    int PermitLimit,
    int WindowMinutes,
    string KeyedBy);

public sealed record AdminJwtSettings(
    int AccessTokenExpiryHours,
    bool IssuerConfigured,
    bool AudienceConfigured);

public sealed record AdminRefreshTokenSettings(
    int ExpiryDays,
    bool RotationEnabled,
    bool RevokeOnPasswordChange,
    bool RevokeOnPasswordReset);

public sealed record AdminSecurityHeadersSettings(
    bool XContentTypeOptionsEnabled,
    bool XFrameOptionsEnabled,
    bool ReferrerPolicyEnabled,
    bool PermissionsPolicyEnabled,
    string CspStatus,
    string HstsStatus);

public sealed record AdminGoogleExternalLoginSettings(
    bool Enabled,
    bool ClientIdConfigured,
    bool ClientSecretConfigured,
    bool AllowAutoLinkByEmail,
    bool AllowStudentAutoProvisioning,
    IReadOnlyList<string> AllowedDomains);

public sealed record AdminExternalLoginSettings(
    AdminGoogleExternalLoginSettings Google);

public sealed record AdminSecuritySettings(
    AdminPasswordPolicySettings PasswordPolicy,
    AdminLockoutSettings Lockout,
    IReadOnlyList<AdminRateLimitPolicyInfo> RateLimitPolicies,
    AdminJwtSettings Jwt,
    AdminRefreshTokenSettings RefreshToken,
    AdminSecurityHeadersSettings SecurityHeaders,
    AdminExternalLoginSettings ExternalLogin);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IAdminSecurityHandler
{
    Task<AdminSecuritySettings> GetSettingsAsync(CancellationToken ct = default);
}
