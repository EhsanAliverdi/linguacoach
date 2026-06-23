using System.Security.Claims;
using LinguaCoach.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ILoginHandler _loginHandler;
    private readonly IChangePasswordHandler _changePasswordHandler;
    private readonly IPasswordResetService _passwordReset;
    private readonly IRefreshTokenService _refreshTokens;

    public AuthController(
        ILoginHandler loginHandler,
        IChangePasswordHandler changePasswordHandler,
        IPasswordResetService passwordReset,
        IRefreshTokenService refreshTokens)
    {
        _loginHandler = loginHandler;
        _changePasswordHandler = changePasswordHandler;
        _passwordReset = passwordReset;
        _refreshTokens = refreshTokens;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthLogin")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _loginHandler.HandleAsync(new LoginCommand(request.Email, request.Password), ct);
            return Ok(new
            {
                token = result.Token,
                role = result.Role.ToString(),
                mustChangePassword = result.MustChangePassword,
                refreshToken = result.RefreshToken,
                refreshExpiresAtUtc = result.RefreshExpiresAtUtc,
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthRefresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers["User-Agent"].ToString();
        var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].ToString();

        var result = await _refreshTokens.RefreshAsync(request.RefreshToken, ip, ua, correlationId, ct);
        if (!result.Succeeded)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        return Ok(new
        {
            token = result.AccessToken,
            refreshToken = result.RefreshToken,
            refreshExpiresAtUtc = result.ExpiresAtUtc,
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthRefresh")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            await _refreshTokens.RevokeAsync(request.RefreshToken, "Logout", ct);

        return NoContent();
    }

    [HttpPost("revoke-sessions")]
    [Authorize]
    public async Task<IActionResult> RevokeSessions(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _refreshTokens.RevokeAllAsync(userId, "UserRevokedAll", ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("AuthChangePassword")]
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

    /// <summary>
    /// Public endpoint — no auth required. Accepts userId + token from reset link,
    /// validates token via ASP.NET Identity, and sets new password.
    /// Returns generic errors to avoid information leakage.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthReset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new { error = "Passwords do not match." });

        var result = await _passwordReset.CompleteResetAsync(
            new CompletePasswordResetCommand(
                request.UserId,
                request.Token,
                request.NewPassword,
                request.ConfirmPassword), ct);

        if (!result.Succeeded)
            return BadRequest(new { error = result.Error ?? "The reset link is invalid or has expired." });

        return NoContent();
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ResetPasswordRequest(string UserId, string Token, string NewPassword, string ConfirmPassword);
