using LinguaCoach.Application.Auth;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetHandler> _logger;

    public PasswordResetHandler(
        LinguaCoachDbContext db,
        UserManager<ApplicationUser> userManager,
        INotificationService notifications,
        IConfiguration configuration,
        ILogger<PasswordResetHandler> logger)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendResetLinkAsync(SendPasswordResetLinkCommand command, CancellationToken ct = default)
    {
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

        var resetLink = $"{baseUrl}/reset-password?userId={Uri.EscapeDataString(user.Id.ToString())}&token={Uri.EscapeDataString(encodedToken)}";

        _logger.LogInformation(
            "Password reset link generated for user {UserId} by admin {AdminId}.",
            user.Id, command.AdminUserId);

        // Queue email — body contains the link but NOT the raw token value separately.
        // If queueing fails, rethrow so the admin knows the request didn't fully complete.
        await _notifications.QueueEmailAsync(
            recipientUserId: profile.UserId,
            title: "Reset your SpeakPath password",
            body: $"An administrator has requested a password reset for your SpeakPath account. " +
                  $"Click the link below to set a new password. This link expires after use.\n\n{resetLink}\n\n" +
                  $"If you did not request this, please contact your administrator.",
            category: NotificationCategory.Account,
            severity: NotificationSeverity.Info,
            ct: ct);
    }

    public async Task<CompletePasswordResetResult> CompleteResetAsync(
        CompletePasswordResetCommand command, CancellationToken ct = default)
    {
        if (command.NewPassword != command.ConfirmPassword)
            return CompletePasswordResetResult.Fail("Passwords do not match.");

        if (command.NewPassword.Length < 8)
            return CompletePasswordResetResult.Fail("Password must be at least 8 characters.");

        // Resolve user by ID (preferred) or by email as fallback.
        ApplicationUser? user = null;
        if (Guid.TryParse(command.UserIdOrEmail, out var userId))
            user = await _userManager.FindByIdAsync(userId.ToString());

        user ??= await _userManager.FindByEmailAsync(command.UserIdOrEmail);

        if (user is null)
        {
            // Do not reveal whether the user exists.
            _logger.LogWarning("CompletePasswordReset: user not found for input '{Input}'.", command.UserIdOrEmail);
            return CompletePasswordResetResult.Fail("The reset link is invalid or has expired.");
        }

        string rawToken;
        try
        {
            rawToken = Base64UrlDecode(command.Token);
        }
        catch
        {
            return CompletePasswordResetResult.Fail("The reset link is invalid or has expired.");
        }

        var result = await _userManager.ResetPasswordAsync(user, rawToken, command.NewPassword);
        if (!result.Succeeded)
        {
            // Log internally but return generic message.
            _logger.LogWarning(
                "CompletePasswordReset failed for user {UserId}: {Errors}",
                user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
            return CompletePasswordResetResult.Fail("The reset link is invalid or has expired.");
        }

        // Clear force-change flag if set.
        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
        return CompletePasswordResetResult.Ok();
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
