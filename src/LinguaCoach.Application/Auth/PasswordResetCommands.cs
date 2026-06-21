namespace LinguaCoach.Application.Auth;

/// <summary>
/// Admin requests a password-reset link to be emailed to a student.
/// The token is generated server-side and never returned to the admin.
/// </summary>
public sealed record SendPasswordResetLinkCommand(
    Guid StudentProfileId,
    Guid AdminUserId);

/// <summary>
/// Student (or automated flow) submits the token from the reset link.
/// </summary>
public sealed record CompletePasswordResetCommand(
    string UserIdOrEmail,
    string Token,
    string NewPassword,
    string ConfirmPassword);

public sealed record CompletePasswordResetResult(bool Succeeded, string? Error)
{
    public static CompletePasswordResetResult Ok() => new(true, null);
    public static CompletePasswordResetResult Fail(string error) => new(false, error);
}

public interface IPasswordResetService
{
    /// <summary>
    /// Generates a reset token, builds a reset link, and queues an email outbox item.
    /// Does not return the token. Throws if student not found.
    /// </summary>
    Task SendResetLinkAsync(SendPasswordResetLinkCommand command, CancellationToken ct = default);

    /// <summary>
    /// Validates the token and changes the password.
    /// Returns a result rather than throwing so the public endpoint can return safe errors.
    /// </summary>
    Task<CompletePasswordResetResult> CompleteResetAsync(CompletePasswordResetCommand command, CancellationToken ct = default);
}
