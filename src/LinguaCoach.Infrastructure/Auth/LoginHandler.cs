using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

namespace LinguaCoach.Infrastructure.Auth;

public sealed class LoginHandler : ILoginHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public LoginHandler(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var valid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!valid)
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!user.EmailConfirmed)
            throw new UnauthorizedAccessException("Account is not active.");

        var token = _tokenService.GenerateToken(user.Id, user.Email!, user.Role);
        return new LoginResult(token, user.Role, user.MustChangePassword);
    }
}
