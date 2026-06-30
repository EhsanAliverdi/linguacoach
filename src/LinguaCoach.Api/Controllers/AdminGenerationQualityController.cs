using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/generation-quality")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminGenerationQualityController : ControllerBase
{
    private readonly IAdminGenerationQualityHandler _handler;
    private readonly IConfiguration _config;

    public AdminGenerationQualityController(IAdminGenerationQualityHandler handler, IConfiguration config)
    {
        _handler = handler;
        _config = config;
    }

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
        var retentionDays = _config.GetValue<int?>("GenerationQuality:RetentionDays") ?? 90;
        var maxRecentDays = Math.Min(retentionDays, 90);

        if (recentDays < 1 || recentDays > maxRecentDays)
            return BadRequest(new { error = $"recentDays must be between 1 and {maxRecentDays}." });

        var s = await _handler.GetSummaryAsync(recentDays, ct);

        return Ok(new
        {
            recentDays,
            retentionDays = s.RetentionDays,
            validationFailureSummary = new
            {
                totalFailures = s.TotalValidationFailures,
                abandonedGenerations = s.AbandonedGenerations,
                failuresLast24Hours = s.RecentFailureCount,
            },
            abandonedWarning = new
            {
                isActive = s.AbandonedWarning.IsActive,
                abandonedRate = s.AbandonedWarning.AbandonedRate,
                abandonedCount = s.AbandonedWarning.AbandonedCount,
                totalFailures = s.AbandonedWarning.TotalFailures,
                warningThreshold = s.AbandonedWarning.WarningThreshold,
                message = s.AbandonedWarning.Message,
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
                providerName = f.ProviderName,
                modelName = f.ModelName,
                correlationId = f.CorrelationId,
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
            providerBreakdown = s.ProviderBreakdown.Select(p => new
            {
                providerName = p.ProviderName,
                modelName = p.ModelName,
                totalFailures = p.TotalFailures,
                abandonedCount = p.AbandonedCount,
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
                contentHashShort = p.ContentHashShort,
                // Full prompt content is intentionally omitted — use GET /api/admin/prompts/{id}
            }),
        });
    }
}
