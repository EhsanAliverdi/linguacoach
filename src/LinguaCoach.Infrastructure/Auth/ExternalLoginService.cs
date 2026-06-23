using LinguaCoach.Application.Auth;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Auth;

/// <summary>
/// Handles Google external login: validates token, links/finds account, issues JWT + refresh token.
///
/// Security invariants:
///   - Google ID token is never stored, logged, or included in audit metadata.
///   - ClientSecret is never exposed through APIs or logs.
///   - Provider disabled → generic rejection.
///   - Unverified email → rejected.
///   - Unknown user → rejected (no auto-provisioning unless AllowStudentAutoProvisioning is true).
///   - Admin accounts are never auto-created via Google login.
///   - AllowAutoLinkByEmail links existing account only if email is verified and config permits.
/// </summary>
public sealed class ExternalLoginService : IExternalLoginService
{
    private const string GoogleProvider = "Google";

    private readonly IGoogleTokenValidator _validator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IAuthSecurityAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly LinguaCoachDbContext _db;
    private readonly IHttpContextAccessor _httpContext;
    private readonly GoogleExternalLoginOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalLoginService> _logger;

    public ExternalLoginService(
        IGoogleTokenValidator validator,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokens,
        IAuthSecurityAuditService audit,
        INotificationService notifications,
        LinguaCoachDbContext db,
        IHttpContextAccessor httpContext,
        IOptions<GoogleExternalLoginOptions> options,
        IConfiguration configuration,
        ILogger<ExternalLoginService> logger)
    {
        _validator = validator;
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokens = refreshTokens;
        _audit = audit;
        _notifications = notifications;
        _db = db;
        _httpContext = httpContext;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ExternalLoginResult> GoogleLoginAsync(ExternalLoginRequest request, CancellationToken ct = default)
    {
        var ip = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var ua = _httpContext.HttpContext?.Request.Headers["User-Agent"].ToString();
        var correlationId = request.CorrelationId;

        if (!_options.Enabled)
        {
            _logger.LogWarning("Google external login attempted but provider is disabled.");
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.ExternalProviderDisabled, AuthEventOutcome.Blocked,
                FailureReasonCode: "ProviderDisabled",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            return ExternalLoginResult.Fail("External login is not available.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            _logger.LogError("Google external login attempted but ClientId is not configured.");
            return ExternalLoginResult.Fail("External login is not configured.");
        }

        // Validate Google ID token — never store the raw token
        var validation = await _validator.ValidateAsync(request.IdToken, _options.ClientId, ct);
        if (!validation.IsValid || validation.Payload is null)
        {
            _logger.LogWarning("Google ID token validation failed: {Reason}", validation.Error);
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.ExternalLoginFailed, AuthEventOutcome.Failure,
                FailureReasonCode: "InvalidToken",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            return ExternalLoginResult.Fail("External login failed.");
        }

        var payload = validation.Payload;

        if (!payload.EmailVerified)
        {
            _logger.LogWarning("Google login rejected — email not verified for subject {Sub}", payload.Subject);
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.ExternalEmailUnverified, AuthEventOutcome.Blocked,
                FailureReasonCode: "EmailNotVerified",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            return ExternalLoginResult.Fail("External login failed.");
        }

        // Enforce allowed-domain restriction if configured
        if (_options.AllowedDomains.Count > 0)
        {
            var hostedDomain = payload.HostedDomain ?? string.Empty;
            var allowed = _options.AllowedDomains
                .Any(d => string.Equals(d, hostedDomain, StringComparison.OrdinalIgnoreCase));
            if (!allowed)
            {
                _logger.LogWarning("Google login rejected — domain '{Domain}' not in allowed list.", hostedDomain);
                await _audit.RecordAsync(new AuthSecurityEventRecord(
                    AuthEventType.ExternalDomainRejected, AuthEventOutcome.Blocked,
                    FailureReasonCode: "DomainNotAllowed",
                    IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
                return ExternalLoginResult.Fail("External login failed.");
            }
        }

        // Find by existing external login (Google sub → UserId link)
        var user = await _userManager.FindByLoginAsync(GoogleProvider, payload.Subject);

        if (user is null)
        {
            // Try auto-link by verified email
            var emailUser = await _userManager.FindByEmailAsync(payload.Email);
            if (emailUser is not null && _options.AllowAutoLinkByEmail)
            {
                // Never auto-create or auto-link admin accounts unless they already exist in Identity
                // (admin accounts do exist — they are created by AdminSeeder, not self-registered)
                var addResult = await _userManager.AddLoginAsync(
                    emailUser,
                    new UserLoginInfo(GoogleProvider, payload.Subject, "Google"));

                if (addResult.Succeeded)
                {
                    user = emailUser;
                    _logger.LogInformation(
                        "Google login: linked existing account UserId={UserId} by email.",
                        user.Id);
                    await _audit.RecordAsync(new AuthSecurityEventRecord(
                        AuthEventType.ExternalLoginLinked, AuthEventOutcome.Success,
                        UserId: user.Id, EmailOrUserName: payload.Email,
                        IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

                    await TryNotifyExternalLoginLinkedAsync(user.Id, payload.Email, payload.DisplayName, ct);
                }
                else
                {
                    _logger.LogWarning("Google login: failed to link existing account — {Errors}",
                        string.Join(", ", addResult.Errors.Select(e => e.Code)));
                    await _audit.RecordAsync(new AuthSecurityEventRecord(
                        AuthEventType.ExternalLoginFailed, AuthEventOutcome.Failure,
                        EmailOrUserName: payload.Email,
                        FailureReasonCode: "LinkFailed",
                        IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
                    return ExternalLoginResult.Fail("External login failed.");
                }
            }
            else if (emailUser is not null && !_options.AllowAutoLinkByEmail)
            {
                _logger.LogWarning("Google login rejected — auto-link disabled for existing email account.");
                await _audit.RecordAsync(new AuthSecurityEventRecord(
                    AuthEventType.ExternalLoginRejected, AuthEventOutcome.Blocked,
                    EmailOrUserName: payload.Email,
                    FailureReasonCode: "AutoLinkDisabled",
                    IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
                return ExternalLoginResult.Fail("External login failed.");
            }
            else
            {
                // Unknown user — reject or auto-provision Student only if explicitly enabled
                if (_options.AllowStudentAutoProvisioning)
                {
                    user = await ProvisionStudentAsync(payload, ip, ua, correlationId, ct);
                    if (user is null)
                        return ExternalLoginResult.Fail("External login failed.");
                }
                else
                {
                    _logger.LogWarning("Google login rejected — no existing account and auto-provisioning is disabled.");
                    await _audit.RecordAsync(new AuthSecurityEventRecord(
                        AuthEventType.ExternalLoginRejected, AuthEventOutcome.Blocked,
                        EmailOrUserName: payload.Email,
                        FailureReasonCode: "NoAccountFound",
                        IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
                    return ExternalLoginResult.Fail("External login failed.");
                }
            }
        }

        // At this point user is resolved. Verify account is active.
        if (!user.EmailConfirmed)
        {
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.ExternalLoginRejected, AuthEventOutcome.Blocked,
                UserId: user.Id, EmailOrUserName: payload.Email,
                FailureReasonCode: "AccountNotActive",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            return ExternalLoginResult.Fail("External login failed.");
        }

        if (user.Role == UserRole.Student)
        {
            var stage = await _db.StudentProfiles
                .Where(p => p.UserId == user.Id)
                .Select(p => (StudentLifecycleStage?)p.LifecycleStage)
                .FirstOrDefaultAsync(ct);

            if (stage == StudentLifecycleStage.Archived)
            {
                await _audit.RecordAsync(new AuthSecurityEventRecord(
                    AuthEventType.ExternalLoginRejected, AuthEventOutcome.Blocked,
                    UserId: user.Id, EmailOrUserName: payload.Email,
                    FailureReasonCode: "AccountArchived",
                    IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
                return ExternalLoginResult.Fail("External login failed.");
            }
        }

        // Reset failed access count on successful external login
        await _userManager.ResetAccessFailedCountAsync(user);

        var accessToken = _tokenService.GenerateToken(user.Id, user.Email!, user.Role);
        var refreshResult = await _refreshTokens.IssueAsync(
            new IssueRefreshTokenCommand(user.Id, ip, ua, correlationId), ct);

        _logger.LogInformation(
            "Google external login succeeded UserId={UserId} Role={Role}",
            user.Id, user.Role);

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.ExternalLoginSucceeded, AuthEventOutcome.Success,
            UserId: user.Id, EmailOrUserName: payload.Email,
            IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

        return ExternalLoginResult.Ok(
            accessToken,
            refreshResult.RefreshToken,
            refreshResult.ExpiresAtUtc,
            user.Role,
            user.MustChangePassword);
    }

    private async Task<ApplicationUser?> ProvisionStudentAsync(
        GoogleTokenPayload payload,
        string? ip,
        string? ua,
        string? correlationId,
        CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = payload.Email,
            Email = payload.Email,
            EmailConfirmed = true,
            Role = UserRole.Student,
            MustChangePassword = false,
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            _logger.LogWarning("Google auto-provisioning failed: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Code)));
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.ExternalLoginFailed, AuthEventOutcome.Failure,
                EmailOrUserName: payload.Email,
                FailureReasonCode: "ProvisioningFailed",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            return null;
        }

        await _userManager.AddToRoleAsync(user, UserRole.Student.ToString());
        await _userManager.AddLoginAsync(user,
            new UserLoginInfo(GoogleProvider, payload.Subject, "Google"));

        _logger.LogInformation(
            "Google auto-provisioned student UserId={UserId} Email={Email}",
            user.Id, payload.Email);

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.ExternalLoginLinked, AuthEventOutcome.Success,
            UserId: user.Id, EmailOrUserName: payload.Email,
            FailureReasonCode: "AutoProvisioned",
            IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

        return user;
    }

    private async Task TryNotifyExternalLoginLinkedAsync(
        Guid userId,
        string? email,
        string? displayName,
        CancellationToken ct)
    {
        try
        {
            var appName = _configuration["PublicApp:AppName"] ?? "SpeakPath";
            var name = displayName ?? email ?? "User";

            await _notifications.QueueInAppAsync(
                recipientUserId: userId,
                title: "Google account linked",
                body: "Your Google account has been linked to your account. You can now sign in with Google.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Info,
                ct: ct);

            await _notifications.QueueEmailAsync(
                recipientUserId: userId,
                title: $"Your {appName} account is now linked to Google",
                body: $"<p>Hello {name},</p><p>Your {appName} account has been linked to your Google account. You can now sign in using Google.</p><p>If you did not do this, contact your administrator immediately.</p><p>— {appName}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Info,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to queue external-login-linked notification for user {UserId}. Auth flow unaffected.",
                userId);
        }
    }
}
