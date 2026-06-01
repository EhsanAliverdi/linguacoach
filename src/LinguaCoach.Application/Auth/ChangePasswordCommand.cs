namespace LinguaCoach.Application.Auth;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword);

public interface IChangePasswordHandler
{
    Task HandleAsync(ChangePasswordCommand command, CancellationToken ct = default);
}
