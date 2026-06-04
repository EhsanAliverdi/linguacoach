using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Auth;

public sealed class LoginHandler : ILoginHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ILogger<LoginHandler> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        // Deliberately not logging email to avoid leaking PII in bulk logs
        _logger.LogInformation("Login attempt for user");

        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            _logger.LogWarning("Login failed — user not found");
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var valid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!valid)
        {
            _logger.LogWarning("Login failed — invalid password for UserId={UserId}", user.Id);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("Login rejected — account not active UserId={UserId}", user.Id);
            throw new UnauthorizedAccessException("Account is not active.");
        }

        var token = _tokenService.GenerateToken(user.Id, user.Email!, user.Role);
        _logger.LogInformation("Login succeeded UserId={UserId} Role={Role} MustChangePassword={MustChange}",
            user.Id, user.Role, user.MustChangePassword);
        return new LoginResult(token, user.Role, user.MustChangePassword);
    }
}
