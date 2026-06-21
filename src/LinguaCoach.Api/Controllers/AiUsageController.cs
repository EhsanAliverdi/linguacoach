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
        [FromQuery] string? provider = null,
        [FromQuery] string? model = null,
        [FromQuery] string? featureKey = null,
        [FromQuery] string? status = null,
        [FromQuery] string? studentId = null,
        CancellationToken ct = default)
    {
        var dateFilter = BuildDateFilter(from, to);
        if (dateFilter is null) return BadRequest(new { error = "from must be before to." });

        Guid? parsedStudentId = null;
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            if (!Guid.TryParse(studentId, out var sid))
                return BadRequest(new { error = $"Invalid studentId '{studentId}'. Must be a valid GUID." });
            parsedStudentId = sid;
        }

        var columnFilter = new AiUsageRecentFilter(
            Provider:   string.IsNullOrWhiteSpace(provider)   ? null : provider.Trim(),
            Model:      string.IsNullOrWhiteSpace(model)      ? null : model.Trim(),
            FeatureKey: string.IsNullOrWhiteSpace(featureKey) ? null : featureKey.Trim(),
            Status:     string.IsNullOrWhiteSpace(status)     ? null : status.Trim(),
            StudentId:  parsedStudentId);

        if (columnFilter.HasInvalidStatus)
            return BadRequest(new { error = $"Invalid status '{status}'. Valid values: success, failed, fallback." });

        var s = await _handler.GetSummaryAsync(dateFilter, columnFilter, ct);
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
        [FromQuery] string? provider = null,
        [FromQuery] string? model = null,
        [FromQuery] string? featureKey = null,
        [FromQuery] string? status = null,
        [FromQuery] string? studentId = null,
        CancellationToken ct = default)
    {
        var dateFilter = BuildDateFilter(from, to);
        if (dateFilter is null) return BadRequest(new { error = "from must be before to." });

        Guid? parsedStudentId = null;
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            if (!Guid.TryParse(studentId, out var sid))
                return BadRequest(new { error = $"Invalid studentId '{studentId}'. Must be a valid GUID." });
            parsedStudentId = sid;
        }

        var recentFilter = new AiUsageRecentFilter(
            Provider:   string.IsNullOrWhiteSpace(provider)   ? null : provider.Trim(),
            Model:      string.IsNullOrWhiteSpace(model)      ? null : model.Trim(),
            FeatureKey: string.IsNullOrWhiteSpace(featureKey) ? null : featureKey.Trim(),
            Status:     string.IsNullOrWhiteSpace(status)     ? null : status.Trim(),
            StudentId:  parsedStudentId);

        if (recentFilter.HasInvalidStatus)
            return BadRequest(new { error = $"Invalid status '{status}'. Valid values: success, failed, fallback." });

        var result = await _handler.GetRecentAsync(page, pageSize, dateFilter, recentFilter, ct);
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

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? provider = null,
        [FromQuery] string? model = null,
        [FromQuery] string? featureKey = null,
        [FromQuery] string? status = null,
        [FromQuery] string? studentId = null,
        CancellationToken ct = default)
    {
        var dateFilter = BuildDateFilter(from, to);
        if (dateFilter is null) return BadRequest(new { error = "from must be before to." });

        Guid? parsedStudentId = null;
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            if (!Guid.TryParse(studentId, out var sid))
                return BadRequest(new { error = $"Invalid studentId '{studentId}'. Must be a valid GUID." });
            parsedStudentId = sid;
        }

        var columnFilter = new AiUsageRecentFilter(
            Provider:   string.IsNullOrWhiteSpace(provider)   ? null : provider.Trim(),
            Model:      string.IsNullOrWhiteSpace(model)      ? null : model.Trim(),
            FeatureKey: string.IsNullOrWhiteSpace(featureKey) ? null : featureKey.Trim(),
            Status:     string.IsNullOrWhiteSpace(status)     ? null : status.Trim(),
            StudentId:  parsedStudentId);

        if (columnFilter.HasInvalidStatus)
            return BadRequest(new { error = $"Invalid status '{status}'. Valid values: success, failed, fallback." });

        var buckets = await _handler.GetTrendsAsync(dateFilter, columnFilter, ct);
        return Ok(buckets.Select(b => new
        {
            date          = b.Date.ToString("yyyy-MM-dd"),
            callCount     = b.CallCount,
            successCount  = b.SuccessCount,
            failureCount  = b.FailureCount,
            fallbackCount = b.FallbackCount,
            inputTokens   = b.InputTokens,
            outputTokens  = b.OutputTokens,
            totalTokens   = b.TotalTokens,
            costUsd       = b.CostUsd,
        }));
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? provider = null,
        [FromQuery] string? model = null,
        [FromQuery] string? featureKey = null,
        [FromQuery] string? status = null,
        [FromQuery] string? studentId = null,
        CancellationToken ct = default)
    {
        var dateFilter = BuildDateFilter(from, to);
        if (dateFilter is null) return BadRequest(new { error = "from must be before to." });

        Guid? parsedStudentId = null;
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            if (!Guid.TryParse(studentId, out var sid))
                return BadRequest(new { error = $"Invalid studentId '{studentId}'. Must be a valid GUID." });
            parsedStudentId = sid;
        }

        var columnFilter = new AiUsageRecentFilter(
            Provider:   string.IsNullOrWhiteSpace(provider)   ? null : provider.Trim(),
            Model:      string.IsNullOrWhiteSpace(model)      ? null : model.Trim(),
            FeatureKey: string.IsNullOrWhiteSpace(featureKey) ? null : featureKey.Trim(),
            Status:     string.IsNullOrWhiteSpace(status)     ? null : status.Trim(),
            StudentId:  parsedStudentId);

        if (columnFilter.HasInvalidStatus)
            return BadRequest(new { error = $"Invalid status '{status}'. Valid values: success, failed, fallback." });

        var rows = await _handler.GetExportAsync(dateFilter, columnFilter, maxRows: 10_000, ct);

        var csv = BuildCsv(rows);
        var filename = $"ai-usage-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", filename);
    }

    private static string BuildCsv(IReadOnlyList<Application.Admin.AiUsageRecentItem> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("CreatedAt,Provider,Model,FeatureKey,StudentId,WasSuccessful,IsFallback,FailureReason,InputTokens,OutputTokens,TotalTokens,CostUsd,DurationMs,CorrelationId");
        foreach (var r in rows)
        {
            sb.Append(CsvEscape(r.CreatedAt.ToString("O"))); sb.Append(',');
            sb.Append(CsvEscape(r.Provider));                sb.Append(',');
            sb.Append(CsvEscape(r.Model));                   sb.Append(',');
            sb.Append(CsvEscape(r.FeatureKey));              sb.Append(',');
            sb.Append(CsvEscape(r.StudentProfileId?.ToString() ?? "")); sb.Append(',');
            sb.Append(r.WasSuccessful ? "true" : "false");  sb.Append(',');
            sb.Append(r.IsFallback    ? "true" : "false");  sb.Append(',');
            sb.Append(CsvEscape(r.FailureReason ?? ""));    sb.Append(',');
            sb.Append(r.InputTokens);                        sb.Append(',');
            sb.Append(r.OutputTokens);                       sb.Append(',');
            sb.Append(r.InputTokens + r.OutputTokens);       sb.Append(',');
            sb.Append(r.CostUsd.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)); sb.Append(',');
            sb.Append(r.DurationMs);                         sb.Append(',');
            sb.AppendLine(CsvEscape(r.CorrelationId ?? ""));
        }
        return sb.ToString();
    }

    // RFC 4180: wrap in quotes if value contains comma, quote, or newline; double up internal quotes.
    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // Returns null when both dates supplied and from >= to (invalid range → 400).
    // Converts unspecified DateTime Kind to UTC.
    private static AiUsageDateFilter? BuildDateFilter(DateTime? from, DateTime? to)
    {
        var utcFrom = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : (DateTime?)null;
        var utcTo   = to.HasValue   ? DateTime.SpecifyKind(to.Value,   DateTimeKind.Utc) : (DateTime?)null;
        var filter  = new AiUsageDateFilter(utcFrom, utcTo);
        return filter.IsInverted ? null : filter;
    }
}
