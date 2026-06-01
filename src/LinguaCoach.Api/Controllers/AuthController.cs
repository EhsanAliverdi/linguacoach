using System.Security.Claims;
using LinguaCoach.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ILoginHandler _loginHandler;
    private readonly IChangePasswordHandler _changePasswordHandler;

    public AuthController(ILoginHandler loginHandler, IChangePasswordHandler changePasswordHandler)
    {
        _loginHandler = loginHandler;
        _changePasswordHandler = changePasswordHandler;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _loginHandler.HandleAsync(new LoginCommand(request.Email, request.Password), ct);
            return Ok(new { token = result.Token, role = result.Role.ToString(), mustChangePassword = result.MustChangePassword });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            await _changePasswordHandler.HandleAsync(new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
