using System.Diagnostics;
using System.Reflection;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Diagnostics;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/diagnostics")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class DiagnosticsController : ControllerBase
{
    private static readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    private readonly DiagnosticEventBuffer _buffer;
    private readonly LinguaCoachDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public DiagnosticsController(
        DiagnosticEventBuffer buffer,
        LinguaCoachDbContext db,
        IConfiguration config,
        IHostEnvironment env)
    {
        _buffer = buffer;
        _db = db;
        _config = config;
        _env = env;
    }

    /// <summary>GET /api/admin/diagnostics/status — system health snapshot.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        bool dbReachable;
        try
        {
            dbReachable = await _db.Database.CanConnectAsync(ct);
        }
        catch
        {
            dbReachable = false;
        }

        // AI status: check the DB-backed category config (llm.default) that the real
        // provider resolver uses — not the legacy AI:WritingFeedback env-var which is never set.
        string? aiProvider = null;
        string? aiModel = null;
        bool aiConfigured = false;
        try
        {
            var defaultLlm = await _db.AiConfigCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryKey == "llm.default", ct);
            if (defaultLlm != null && defaultLlm.IsConfigured)
            {
                aiProvider = defaultLlm.ProviderName;
                aiModel = defaultLlm.ModelName;
                aiConfigured = true;
            }
        }
        catch { /* db already checked above; silently leave aiConfigured false */ }

        var logLevel = _config["LOG_LEVEL"] ?? _config["Logging:LogLevel:Default"] ?? "Information";

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        var uptime = (long)(DateTimeOffset.UtcNow - _startTime).TotalSeconds;

        return Ok(new
        {
            environment = _env.EnvironmentName,
            version,
            serverTimeUtc = DateTimeOffset.UtcNow,
            uptimeSeconds = uptime,
            logLevel,
            diagnosticEventsEnabled = _buffer.IsEnabled,
            diagnosticEventCount = _buffer.Count,
            database = new { reachable = dbReachable },
            ai = new
            {
                providerConfigured = aiConfigured,
                activeProvider = aiConfigured ? aiProvider : null,
                activeModel = aiConfigured ? aiModel : null,
                // Never return API keys — only configured status
            }
        });
    }

    /// <summary>GET /api/admin/diagnostics/events — recent diagnostic event log.</summary>
    [HttpGet("events")]
    public IActionResult GetEvents(
        [FromQuery] string? level = null,
        [FromQuery] string? category = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? q = null,
        [FromQuery] int limit = 100)
    {
        if (!_buffer.IsEnabled)
            return Ok(new { enabled = false, items = Array.Empty<object>() });

        var items = _buffer.Query(level, category, correlationId, q, limit);

        return Ok(new
        {
            enabled = true,
            total = items.Count,
            items = items.Select(e => new
            {
                timestampUtc = e.TimestampUtc,
                level = e.Level,
                category = e.Category,
                message = e.Message,
                correlationId = e.CorrelationId,
                userId = e.UserId,
                path = e.Path,
                statusCode = e.StatusCode,
                elapsedMs = e.ElapsedMs,
            })
        });
    }
}
