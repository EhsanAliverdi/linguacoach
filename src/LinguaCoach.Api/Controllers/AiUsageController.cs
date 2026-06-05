using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/ai-usage")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AiUsageController : ControllerBase
{
    private readonly IAdminAiUsageHandler _handler;

    public AiUsageController(IAdminAiUsageHandler handler) => _handler = handler;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var s = await _handler.GetSummaryAsync(ct);
        return Ok(new
        {
            totalCalls = s.TotalCalls,
            successfulCalls = s.SuccessfulCalls,
            failedCalls = s.FailedCalls,
            fallbackCalls = s.FallbackCalls,
            totalCostUsd = s.TotalCostUsd,
            successRate = s.TotalCalls > 0
                ? Math.Round((double)s.SuccessfulCalls / s.TotalCalls * 100, 1)
                : 0,
            byProvider = s.ByProvider.Select(p => new
            {
                provider = p.Provider,
                calls = p.Calls,
                successful = p.Successful,
                fallback = p.Fallback,
                costUsd = p.CostUsd,
            }),
            byFeature = s.ByFeature.Select(f => new
            {
                feature = f.Feature,
                calls = f.Calls,
                successful = f.Successful,
                costUsd = f.CostUsd,
            }),
        });
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var items = await _handler.GetRecentAsync(limit, ct);
        return Ok(new
        {
            total = items.Count,
            items = items.Select(i => new
            {
                id = i.Id,
                createdAt = i.CreatedAt,
                studentProfileId = i.StudentProfileId,
                featureKey = i.FeatureKey,
                provider = i.Provider,
                model = i.Model,
                isFallback = i.IsFallback,
                wasSuccessful = i.WasSuccessful,
                failureReason = i.FailureReason,
                inputTokens = i.InputTokens,
                outputTokens = i.OutputTokens,
                costUsd = i.CostUsd,
                durationMs = i.DurationMs,
                correlationId = i.CorrelationId,
            }),
        });
    }
}
