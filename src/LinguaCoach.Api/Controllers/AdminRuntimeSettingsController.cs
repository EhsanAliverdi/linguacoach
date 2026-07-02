using System.Security.Claims;
using System.Text.Json;
using LinguaCoach.Application.Admin.RuntimeSettings;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase 20B: admin control plane for typed runtime settings / feature gates. Only
/// registry-known keys may be read or changed; secrets, provider keys, and connection
/// strings are never part of this registry.
/// </summary>
[ApiController]
[Route("api/admin/runtime-settings")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminRuntimeSettingsController : ControllerBase
{
    private readonly IRuntimeSettingsService _service;

    public AdminRuntimeSettingsController(IRuntimeSettingsService service)
    {
        _service = service;
    }

    private Guid AdminUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("No admin user id in token."));

    [HttpGet("feature-gates")]
    public async Task<IActionResult> GetFeatureGates(CancellationToken ct)
    {
        var groups = await _service.GetAllAsync(ct);
        return Ok(groups);
    }

    [HttpGet("feature-gates/{key}")]
    public async Task<IActionResult> GetFeatureGate(string key, CancellationToken ct)
    {
        var group = await _service.GetByKeyAsync(key, ct);
        if (group is null) return NotFound(new { error = $"Unknown feature gate group '{key}'." });
        return Ok(group);
    }

    [HttpPut("feature-gates/{key}/settings")]
    public async Task<IActionResult> UpdateFeatureGate(
        string key, [FromBody] UpdateFeatureGateRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateFeatureGateGroupCommand(
                key, AdminUserId, request.Values ?? new Dictionary<string, JsonElement>(),
                request.Reason ?? string.Empty, request.ConfirmationText);
            var updated = await _service.UpdateAsync(command, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("feature-gates/{key}/override")]
    public async Task<IActionResult> ResetFeatureGateOverride(
        string key, [FromBody] ResetFeatureGateRequest request, CancellationToken ct)
    {
        try
        {
            var command = new ResetFeatureGateGroupCommand(key, AdminUserId, request.Reason ?? string.Empty);
            var updated = await _service.ResetAsync(command, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public sealed record UpdateFeatureGateRequest(
    Dictionary<string, JsonElement>? Values,
    string? Reason,
    string? ConfirmationText);

public sealed record ResetFeatureGateRequest(string? Reason);
