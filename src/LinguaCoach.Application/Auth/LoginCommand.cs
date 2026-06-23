using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Auth;

public sealed record LoginCommand(string Email, string Password);

public sealed record LoginResult(string Token, UserRole Role, bool MustChangePassword, string? RefreshToken = null, DateTime? RefreshExpiresAtUtc = null);

public interface ILoginHandler
{
    Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken ct = default);
}
