using System.Security.Claims;
using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminUsageGovernanceController : ControllerBase
{
    private readonly IUsageGovernanceAdminService _governance;
    private readonly IUsageQuotaService _quota;

    public AdminUsageGovernanceController(
        IUsageGovernanceAdminService governance,
        IUsageQuotaService quota)
    {
        _governance = governance;
        _quota = quota;
    }

    private Guid AdminUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("No admin user id in token."));

    // ── Feature definitions ──────────────────────────────────────────────────

    [HttpGet("feature-definitions")]
    public async Task<IActionResult> ListFeatureDefinitions(CancellationToken ct)
    {
        var defs = await _governance.ListFeatureDefinitionsAsync(ct);
        return Ok(defs.Select(d => new
        {
            d.Id,
            d.Key,
            d.Name,
            d.Description,
            Category = d.Category.ToString(),
            DefaultEnforcementMode = d.DefaultEnforcementMode.ToString(),
            UnitType = d.UnitType.ToString(),
            d.IsExpensive,
            d.IsStudentVisible,
            d.IsEnabledByDefault,
            d.CreatedAt,
            d.UpdatedAt
        }));
    }

    // ── Usage policies ───────────────────────────────────────────────────────

    [HttpGet("usage-policies")]
    public async Task<IActionResult> ListUsagePolicies(CancellationToken ct)
    {
        var policies = await _governance.ListUsagePoliciesAsync(ct);
        return Ok(policies.Select(MapPolicy));
    }

    [HttpGet("usage-policies/{id:guid}")]
    public async Task<IActionResult> GetUsagePolicy(Guid id, CancellationToken ct)
    {
        var policy = await _governance.GetUsagePolicyAsync(id, ct);
        return policy is null ? NotFound() : Ok(MapPolicy(policy));
    }

    [HttpPost("usage-policies")]
    public async Task<IActionResult> CreateUsagePolicy(
        [FromBody] CreateUsagePolicyRequest request,
        CancellationToken ct)
    {
        var policy = await _governance.CreateUsagePolicyAsync(request, AdminUserId, ct);
        return CreatedAtAction(nameof(GetUsagePolicy), new { id = policy.Id }, MapPolicy(policy));
    }

    [HttpPut("usage-policies/{id:guid}")]
    public async Task<IActionResult> UpdateUsagePolicy(
        Guid id,
        [FromBody] UpdateUsagePolicyRequest request,
        CancellationToken ct)
    {
        try
        {
            var policy = await _governance.UpdateUsagePolicyAsync(id, request, AdminUserId, ct);
            return Ok(MapPolicy(policy));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ── Student policy assignment ────────────────────────────────────────────

    [HttpPut("students/{studentId:guid}/usage-policy")]
    public async Task<IActionResult> AssignStudentPolicy(
        Guid studentId,
        [FromBody] AssignStudentPolicyRequest request,
        CancellationToken ct)
    {
        try
        {
            await _governance.AssignPolicyToStudentAsync(studentId, request.PolicyId, AdminUserId, request.Reason, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("students/{studentId:guid}/usage-policy")]
    public async Task<IActionResult> GetStudentEffectivePolicy(Guid studentId, CancellationToken ct)
    {
        var policy = await _governance.GetStudentEffectivePolicyAsync(studentId, ct);
        return policy is null ? Ok(null) : Ok(MapPolicy(policy));
    }

    // ── Student usage summary ────────────────────────────────────────────────

    [HttpGet("students/{studentId:guid}/usage")]
    public async Task<IActionResult> GetStudentUsage(
        Guid studentId,
        [FromQuery] string? period,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        DateOnly fromDate, toDate;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (from is not null && to is not null
            && DateOnly.TryParse(from, out fromDate)
            && DateOnly.TryParse(to, out toDate))
        {
            // explicit range
        }
        else if (period == "month")
        {
            fromDate = new DateOnly(today.Year, today.Month, 1);
            toDate = today;
        }
        else // default: today
        {
            fromDate = toDate = today;
        }

        var summary = await _quota.GetUsageSummaryAsync(studentId, fromDate, toDate, ct);
        return Ok(summary);
    }

    // ── Mappings ─────────────────────────────────────────────────────────────

    private static object MapPolicy(LinguaCoach.Domain.Entities.UsagePolicy p) => new
    {
        p.Id,
        p.Name,
        p.Description,
        ScopeType = p.ScopeType.ToString(),
        p.IsDefault,
        p.IsActive,
        p.CreatedAt,
        p.UpdatedAt,
        Rules = p.Rules.Select(r => new
        {
            r.Id,
            r.FeatureKey,
            r.TrackingEnabled,
            EnforcementMode = r.EnforcementMode.ToString(),
            UnitType = r.UnitType.ToString(),
            r.DailyLimit,
            r.WeeklyLimit,
            r.MonthlyLimit,
            r.DailyCostLimit,
            r.MonthlyCostLimit,
            r.WarningThresholdPercent,
            r.IsActive
        })
    };
}

public sealed record AssignStudentPolicyRequest(Guid PolicyId, string? Reason);
