using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Auth;

public sealed class LoginHandler : ILoginHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        LinguaCoachDbContext db,
        ILogger<LoginHandler> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _db = db;
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

        // Check lockout before validating password — do not reveal reason
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login rejected — account locked out UserId={UserId}", user.Id);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var valid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!valid)
        {
            // Increment failed count; Identity will set LockoutEnd when threshold is reached
            await _userManager.AccessFailedAsync(user);
            var stillLockedOut = await _userManager.IsLockedOutAsync(user);
            _logger.LogWarning(
                "Login failed — invalid password UserId={UserId} LockedOut={LockedOut}",
                user.Id, stillLockedOut);
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
        return new LoginResult(token, user.Role, user.MustChangePassword);
    }
}
