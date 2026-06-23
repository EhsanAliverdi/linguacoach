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

namespace LinguaCoach.Infrastructure.Auth;

public sealed class LoginHandler : ILoginHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly LinguaCoachDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IAuthSecurityAuditService _audit;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        LinguaCoachDbContext db,
        INotificationService notifications,
        IAuthSecurityAuditService audit,
        IHttpContextAccessor httpContext,
        IConfiguration configuration,
        ILogger<LoginHandler> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _db = db;
        _notifications = notifications;
        _audit = audit;
        _httpContext = httpContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        // Deliberately not logging email to avoid leaking PII in bulk logs
        _logger.LogInformation("Login attempt for user");

        var ip = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var ua = _httpContext.HttpContext?.Request.Headers["User-Agent"].ToString();
        var correlationId = _httpContext.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();

        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            _logger.LogWarning("Login failed — user not found");
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.LoginFailed, AuthEventOutcome.Failure,
                EmailOrUserName: command.Email,
                FailureReasonCode: "UnknownUserGeneric",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        // Check lockout before validating password — do not reveal reason
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login rejected — account locked out UserId={UserId}", user.Id);
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.LoginLockedOut, AuthEventOutcome.Blocked,
                UserId: user.Id, EmailOrUserName: command.Email,
                FailureReasonCode: "LockedOut",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var valid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!valid)
        {
            await _userManager.AccessFailedAsync(user);
            var stillLockedOut = await _userManager.IsLockedOutAsync(user);
            _logger.LogWarning(
                "Login failed — invalid password UserId={UserId} LockedOut={LockedOut}",
                user.Id, stillLockedOut);
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.LoginFailed, AuthEventOutcome.Failure,
                UserId: user.Id, EmailOrUserName: command.Email,
                FailureReasonCode: "InvalidCredentials",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

            // Notify only on lockout transition — not on every failed attempt or every locked-out attempt.
            if (stillLockedOut)
                await TryNotifyAccountLockedAsync(user.Id, user.Email, ct);

            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("Login rejected — account not active UserId={UserId}", user.Id);
            throw new UnauthorizedAccessException("Account is not active.");
        }

        if (user.Role == UserRole.Student)
        {
            var lifecycleStage = await _db.StudentProfiles
                .Where(p => p.UserId == user.Id)
                .Select(p => (StudentLifecycleStage?)p.LifecycleStage)
                .FirstOrDefaultAsync(ct);

            if (lifecycleStage == StudentLifecycleStage.Archived)
            {
                _logger.LogWarning("Login rejected - archived student UserId={UserId}", user.Id);
                throw new UnauthorizedAccessException("Account is not active.");
            }
        }

        // Successful login — reset failed access count
        await _userManager.ResetAccessFailedCountAsync(user);

        var token = _tokenService.GenerateToken(user.Id, user.Email!, user.Role);
        _logger.LogInformation("Login succeeded UserId={UserId} Role={Role} MustChangePassword={MustChange}",
            user.Id, user.Role, user.MustChangePassword);

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.LoginSucceeded, AuthEventOutcome.Success,
            UserId: user.Id, EmailOrUserName: command.Email,
            IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

        return new LoginResult(token, user.Role, user.MustChangePassword);
    }

    private async Task TryNotifyAccountLockedAsync(Guid userId, string? email, CancellationToken ct)
    {
        try
        {
            var appName = _configuration["PublicApp:AppName"] ?? "SpeakPath";
            var displayName = email ?? "User";

            await _notifications.QueueInAppAsync(
                recipientUserId: userId,
                title: "Account temporarily locked",
                body: "Your account has been temporarily locked due to repeated unsuccessful login attempts. It will unlock automatically. Contact your administrator if you need immediate access.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                ct: ct);

            await _notifications.QueueEmailAsync(
                recipientUserId: userId,
                title: $"Your {appName} account has been temporarily locked",
                body: $"<p>Hello {displayName},</p><p>Your {appName} account has been temporarily locked due to repeated unsuccessful login attempts.</p><p>Your account will unlock automatically after a short period. If you need immediate access, please contact your administrator.</p><p>If you did not attempt to log in, your account credentials may be at risk — contact your administrator immediately.</p><p>— {appName}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to queue account-locked security notification for user {UserId}. Auth flow unaffected.",
                userId);
        }
    }
}
