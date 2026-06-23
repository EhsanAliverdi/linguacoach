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

/// <summary>
/// Implements token-based password reset using ASP.NET Identity's built-in token provider.
///
/// Security invariants:
///   - Token is never logged, returned to admin, or stored in notification metadata.
///   - Reset link contains Base64Url-encoded token embedded in the URL query string.
///   - CompleteResetAsync returns a generic error on invalid token (no info leak).
///   - Existing temp-password flow is unaffected.
/// </summary>
public sealed class PasswordResetHandler : IPasswordResetService
{
    private readonly LinguaCoachDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifications;
    private readonly INotificationTemplateRenderer _templateRenderer;
    private readonly IAuthSecurityAuditService _audit;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetHandler> _logger;

    public PasswordResetHandler(
        LinguaCoachDbContext db,
        UserManager<ApplicationUser> userManager,
        INotificationService notifications,
        INotificationTemplateRenderer templateRenderer,
        IAuthSecurityAuditService audit,
        IHttpContextAccessor httpContext,
        IConfiguration configuration,
        ILogger<PasswordResetHandler> logger)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
        _templateRenderer = templateRenderer;
        _audit = audit;
        _httpContext = httpContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendResetLinkAsync(SendPasswordResetLinkCommand command, CancellationToken ct = default)
    {
        var ip = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var ua = _httpContext.HttpContext?.Request.Headers["User-Agent"].ToString();
        var correlationId = _httpContext.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();

        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var user = await _userManager.FindByIdAsync(profile.UserId.ToString())
            ?? throw new InvalidOperationException("Student user not found.");

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new InvalidOperationException("Student has no email address on file.");

        // Generate Identity reset token — never log or return this value.
        var rawToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = Base64UrlEncode(rawToken);

        var baseUrl = _configuration["PublicApp:BaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:4200";
        var appName = _configuration["PublicApp:AppName"] ?? "SpeakPath";
        var resetLink = $"{baseUrl}/reset-password?userId={Uri.EscapeDataString(user.Id.ToString())}&token={Uri.EscapeDataString(encodedToken)}";

        _logger.LogInformation(
            "Password reset link generated for user {UserId} by admin {AdminId}.",
            user.Id, command.AdminUserId);

        var displayName = !string.IsNullOrWhiteSpace(user.Email) ? user.Email : "Student";
        var (emailSubject, emailBody) = await ResolveResetEmailContentAsync(resetLink, appName, displayName, ct);

        // Queue email — body contains the link but NOT the raw token value separately.
        await _notifications.QueueEmailAsync(
            recipientUserId: profile.UserId,
            title: emailSubject,
            body: emailBody,
            category: NotificationCategory.Account,
            severity: NotificationSeverity.Info,
            ct: ct);

        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.PasswordResetRequested, AuthEventOutcome.Requested,
            UserId: user.Id, EmailOrUserName: user.Email,
            IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

        // In-app only — the reset-link email already serves as the email notification.
        await TryNotifyResetRequestedAsync(profile.UserId, ct);
    }

    private async Task<(string Subject, string Body)> ResolveResetEmailContentAsync(
        string resetLink, string appName, string displayName, CancellationToken ct)
    {
        var template = await _db.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.TemplateKey == "account.password_reset" &&
                t.Channel == NotificationChannel.Email &&
                t.IsActive, ct);

        if (template is null)
        {
            _logger.LogWarning(
                "Active template 'account.password_reset'/Email not found. Using fallback content.");
            return (
                "Reset your SpeakPath password",
                $"An administrator has requested a password reset for your SpeakPath account. " +
                $"Click the link below to set a new password. This link expires after use.\n\n{resetLink}\n\n" +
                $"If you did not request this, please contact your administrator."
            );
        }

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DisplayName"] = displayName,
            ["ResetLink"] = resetLink,
            ["AppName"] = appName,
        };

        var rendered = _templateRenderer.Render(template.Subject, template.Title, template.Body, variables);

        if (rendered.MissingVariables.Count > 0)
            _logger.LogWarning(
                "Template 'account.password_reset'/Email has missing variables: {Vars}",
                string.Join(", ", rendered.MissingVariables));

        return (rendered.RenderedSubject ?? "Reset your password", rendered.RenderedBody);
    }

    public async Task<CompletePasswordResetResult> CompleteResetAsync(
        CompletePasswordResetCommand command, CancellationToken ct = default)
    {
        var ip = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var ua = _httpContext.HttpContext?.Request.Headers["User-Agent"].ToString();
        var correlationId = _httpContext.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();

        if (command.NewPassword != command.ConfirmPassword)
            return CompletePasswordResetResult.Fail("Passwords do not match.");

        // Resolve user by ID (preferred) or by email as fallback.
        ApplicationUser? user = null;
        if (Guid.TryParse(command.UserIdOrEmail, out var userId))
            user = await _userManager.FindByIdAsync(userId.ToString());
        user ??= await _userManager.FindByEmailAsync(command.UserIdOrEmail);

        if (user is null)
        {
            // Do not reveal whether the user exists.
            _logger.LogWarning("CompletePasswordReset: user not found for input '{Input}'.", command.UserIdOrEmail);
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.PasswordResetFailed, AuthEventOutcome.Failure,
                FailureReasonCode: "UnknownUserGeneric",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            return CompletePasswordResetResult.Fail("The reset link is invalid or has expired.");
        }

        string rawToken;
        try
        {
            rawToken = Base64UrlDecode(command.Token);
        }
        catch
        {
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.PasswordResetFailed, AuthEventOutcome.Failure,
                UserId: user.Id, EmailOrUserName: user.Email,
                FailureReasonCode: "InvalidOrExpiredToken",
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            return CompletePasswordResetResult.Fail("The reset link is invalid or has expired.");
        }

        var result = await _userManager.ResetPasswordAsync(user, rawToken, command.NewPassword);
        if (!result.Succeeded)
        {
            var reasonCode = result.Errors.Any(e => e.Code.Contains("Password", StringComparison.OrdinalIgnoreCase))
                ? "PasswordPolicyFailed"
                : "InvalidOrExpiredToken";
            _logger.LogWarning(
                "CompletePasswordReset failed for user {UserId}: {Errors}",
                user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
            await _audit.RecordAsync(new AuthSecurityEventRecord(
                AuthEventType.PasswordResetFailed, AuthEventOutcome.Failure,
                UserId: user.Id, EmailOrUserName: user.Email,
                FailureReasonCode: reasonCode,
                IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);
            return CompletePasswordResetResult.Fail("The reset link is invalid or has expired.");
        }

        // Clear force-change flag if set.
        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.PasswordResetSucceeded, AuthEventOutcome.Success,
            UserId: user.Id, EmailOrUserName: user.Email,
            IpAddress: ip, UserAgent: ua, CorrelationId: correlationId), ct);

        await TryNotifyResetSucceededAsync(user.Id, user.Email, ct);
        return CompletePasswordResetResult.Ok();
    }

    private async Task TryNotifyResetRequestedAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            await _notifications.QueueInAppAsync(
                recipientUserId: userId,
                title: "Password reset requested",
                body: "A password reset was requested for your account. If this was not you, contact your administrator.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to queue password-reset-requested in-app notification for user {UserId}. Auth flow unaffected.",
                userId);
        }
    }

    private async Task TryNotifyResetSucceededAsync(Guid userId, string? email, CancellationToken ct)
    {
        try
        {
            var appName = _configuration["PublicApp:AppName"] ?? "SpeakPath";
            var displayName = email ?? "User";

            await _notifications.QueueInAppAsync(
                recipientUserId: userId,
                title: "Password reset successful",
                body: "Your password was reset successfully. If you did not do this, contact your administrator immediately.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                ct: ct);

            await _notifications.QueueEmailAsync(
                recipientUserId: userId,
                title: $"Your {appName} password was reset",
                body: $"<p>Hello {displayName},</p><p>Your {appName} password was reset successfully.</p><p>If you did not request this reset, please contact your administrator immediately.</p><p>— {appName}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to queue password-reset-succeeded notification for user {UserId}. Auth flow unaffected.",
                userId);
        }
    }

    private static string Base64UrlEncode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Base64UrlDecode(string value)
    {
        var padded = value
            .Replace('-', '+')
            .Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "=";  break;
        }
        var bytes = Convert.FromBase64String(padded);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
