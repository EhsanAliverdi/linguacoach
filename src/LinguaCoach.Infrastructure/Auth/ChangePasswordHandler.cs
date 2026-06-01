using LinguaCoach.Application.Auth;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

namespace LinguaCoach.Infrastructure.Auth;

public sealed class ChangePasswordHandler : IChangePasswordHandler
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ChangePasswordHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task HandleAsync(ChangePasswordCommand command, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (user is null)
            throw new InvalidOperationException("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
    }
}
