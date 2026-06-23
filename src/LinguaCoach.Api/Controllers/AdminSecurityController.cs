using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/security")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminSecurityController : ControllerBase
{
    private readonly IAdminSecurityHandler _securityHandler;
    private readonly IAdminAuthEventHandler _authEvents;

    public AdminSecurityController(
        IAdminSecurityHandler securityHandler,
        IAdminAuthEventHandler authEvents)
    {
        _securityHandler = securityHandler;
        _authEvents = authEvents;
    }

    /// <summary>
    /// Returns the current security configuration as a safe read model.
    /// Secrets (JWT key, Google ClientSecret) are never included — only configured yes/no.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _securityHandler.GetSettingsAsync(ct));

    /// <summary>
    /// Returns paginated auth security events. Alias for /api/admin/auth-events,
    /// co-located under the security namespace for the admin security page.
    /// </summary>
    [HttpGet("auth-events")]
    public async Task<IActionResult> ListAuthEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? userId = null,
        [FromQuery] string? email = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? outcome = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        Guid? userIdGuid = Guid.TryParse(userId, out var g) ? g : null;
        var result = await _authEvents.ListAsync(new AdminAuthEventListQuery(
            Page: page,
            PageSize: Math.Min(pageSize, 100),
            UserId: userIdGuid,
            EmailSearch: email,
            EventType: eventType,
            Outcome: outcome,
            From: from,
            To: to), ct);
        return Ok(result);
    }
}
