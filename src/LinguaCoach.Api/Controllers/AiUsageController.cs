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
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var filter = BuildFilter(from, to);
        if (filter is null) return BadRequest(new { error = "from must be before to." });
        var s = await _handler.GetSummaryAsync(filter, ct);
        return Ok(new
        {
            totalCalls = s.TotalCalls,
            successfulCalls = s.SuccessfulCalls,
            failedCalls = s.FailedCalls,
            fallbackCalls = s.FallbackCalls,
            totalCostUsd = s.TotalCostUsd,
            totalInputTokens = s.TotalInputTokens,
            totalOutputTokens = s.TotalOutputTokens,
            totalTokens = s.TotalTokens,
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
    public async Task<IActionResult> GetRecent(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var filter = BuildFilter(from, to);
        if (filter is null) return BadRequest(new { error = "from must be before to." });
        var result = await _handler.GetRecentAsync(page, pageSize, filter, ct);
        return Ok(new
        {
            items = result.Items.Select(i => new
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
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            totalPages = result.TotalPages,
        });
    }

    // Returns null when both dates supplied and from >= to (invalid range → 400).
    // Converts unspecified DateTime Kind to UTC.
    private static AiUsageDateFilter? BuildFilter(DateTime? from, DateTime? to)
    {
        var utcFrom = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : (DateTime?)null;
        var utcTo   = to.HasValue   ? DateTime.SpecifyKind(to.Value,   DateTimeKind.Utc) : (DateTime?)null;
        var filter  = new AiUsageDateFilter(utcFrom, utcTo);
        return filter.IsInverted ? null : filter;
    }
}
