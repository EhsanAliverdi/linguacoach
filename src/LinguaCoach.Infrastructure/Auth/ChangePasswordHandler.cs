using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace LinguaCoach.Infrastructure.Auth;

public sealed class ChangePasswordHandler : IChangePasswordHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthSecurityAuditService _audit;
    private readonly IHttpContextAccessor _httpContext;

    public ChangePasswordHandler(
        UserManager<ApplicationUser> userManager,
        IAuthSecurityAuditService audit,
        IHttpContextAccessor httpContext)
    {
        _userManager = userManager;
        _audit = audit;
        _httpContext = httpContext;
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
    }
}
