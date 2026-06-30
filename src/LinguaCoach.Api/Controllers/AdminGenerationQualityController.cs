using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/generation-quality")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminGenerationQualityController : ControllerBase
{
    private readonly IAdminGenerationQualityHandler _handler;

    public AdminGenerationQualityController(IAdminGenerationQualityHandler handler)
        => _handler = handler;

    /// <summary>
    /// GET /api/admin/generation-quality/summary
    /// Returns prompt version metadata and content validation failure diagnostics.
    /// recentDays defaults to 30. Max 90.
    /// Secrets are never returned — no provider API keys, no storage keys, no raw AI output.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int recentDays = 30,
        CancellationToken ct = default)
    {
        if (recentDays < 1 || recentDays > 90)
            return BadRequest(new { error = "recentDays must be between 1 and 90." });

        var s = await _handler.GetSummaryAsync(recentDays, ct);

        return Ok(new
        {
            recentDays,
            validationFailureSummary = new
            {
                totalFailures = s.TotalValidationFailures,
                abandonedGenerations = s.AbandonedGenerations,
                failuresLast24Hours = s.RecentFailureCount,
            },
            latestFailures = s.LatestFailures.Select(f => new
            {
                timestampUtc = f.TimestampUtc,
                patternKey = f.PatternKey,
                activityTypeName = f.ActivityTypeName,
                cefrLevel = f.CefrLevel,
                objectiveKey = f.ObjectiveKey,
                validationErrors = f.ValidationErrors,
                attemptNumber = f.AttemptNumber,
            }),
            patternFailureBreakdown = s.PatternBreakdown.Select(p => new
            {
                patternKey = p.PatternKey,
                totalFailures = p.TotalFailures,
                abandonedCount = p.AbandonedCount,
                latestError = p.LatestError,
            }),
            cefrFailureBreakdown = s.CefrBreakdown.Select(c => new
            {
                cefrLevel = c.CefrLevel,
                totalFailures = c.TotalFailures,
            }),
            promptSummary = s.PromptSummary.Select(p => new
            {
                id = p.Id,
                key = p.Key,
                version = p.Version,
                isActive = p.IsActive,
                maxInputTokens = p.MaxInputTokens,
                maxOutputTokens = p.MaxOutputTokens,
                seededAtUtc = p.SeededAtUtc,
                // Content is intentionally omitted — use GET /api/admin/prompts/{id} for full content
            }),
        });
    }
}
