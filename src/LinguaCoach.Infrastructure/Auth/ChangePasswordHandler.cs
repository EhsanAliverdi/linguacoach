using LinguaCoach.Application.Auth;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Auth;

public sealed class ChangePasswordHandler : IChangePasswordHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly INotificationService _notifications;
    private readonly IAuthSecurityAuditService _audit;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChangePasswordHandler> _logger;

    public ChangePasswordHandler(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokens,
        INotificationService notifications,
        IAuthSecurityAuditService audit,
        IHttpContextAccessor httpContext,
        IConfiguration configuration,
        ILogger<ChangePasswordHandler> logger)
    {
        _userManager = userManager;
        _refreshTokens = refreshTokens;
        _notifications = notifications;
        _audit = audit;
        _httpContext = httpContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(ChangePasswordCommand command, CancellationToken ct = default)
    {
        var ip = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var ua = _httpContext.HttpContext?.Request.Headers["User-Agent"].ToString();
        var correlationId = _httpContext.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();

        var user = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (user is null)
            throw new InvalidOperationException("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
        {
            var reasonCode = result.Errors.Any(e => e.Code.Contains("Password", StringComparison.OrdinalIgnoreCase))
                ? "PasswordPolicyFailed"
                : "InvalidCredentials";

            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.PasswordChangeFailed, AuthEventOutcome.Failure,
                UserId: command.UserId,
                FailureReasonCode: reasonCode,
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        var wasForcedChange = user.MustChangePassword;
        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);

        var eventType = wasForcedChange
            ? AuthEventType.ForcePasswordChangeCompleted
            : AuthEventType.PasswordChanged;

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            eventType, AuthEventOutcome.Success,
            UserId: command.UserId, EmailOrUserName: user.Email,
            IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

        // Revoke all refresh token sessions — password change invalidates all sessions.
        await _refreshTokens.RevokeAllAsync(command.UserId, "PasswordChanged", ct);

        // Queue security notifications — non-fatal; never include password/token.
        await TryNotifyPasswordChangedAsync(command.UserId, user.Email, ct);
    }

    private async Task TryNotifyPasswordChangedAsync(Guid userId, string? email, CancellationToken ct)
    {
        try
        {
            var appName = _configuration["PublicApp:AppName"] ?? "SpeakPath";
            var displayName = email ?? "User";

            await _notifications.QueueInAppAsync(
                recipientUserId: userId,
                title: "Password changed",
                body: "Your password was changed successfully. If you did not make this change, contact your administrator immediately.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                ct: ct);

            await _notifications.QueueEmailAsync(
                recipientUserId: userId,
                title: $"Your {appName} password was changed",
                body: $"<p>Hello {displayName},</p><p>Your {appName} password was changed successfully.</p><p>If you did not make this change, please contact your administrator immediately.</p><p>— {appName}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to queue password-changed security notification for user {UserId}. Auth flow unaffected.",
                userId);
        }
    }
}
