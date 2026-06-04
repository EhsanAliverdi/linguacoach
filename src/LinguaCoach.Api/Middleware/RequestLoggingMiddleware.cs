using System.Diagnostics;
using System.Security.Claims;

namespace LinguaCoach.Api.Middleware;

/// <summary>
/// Logs every HTTP request with method, path, status, elapsed ms, userId, role, correlationId.
/// Never logs Authorization/Cookie headers, passwords, or tokens.
/// Request body logging is disabled by default and only allowed in Development.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private static readonly HashSet<string> SkippedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health", "/favicon.ico",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly bool _enableRequestBodyLogging;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IConfiguration config)
    {
        _next = next;
        _logger = logger;
        _enableRequestBodyLogging = config.GetValue<bool>("ENABLE_REQUEST_BODY_LOGS", false);
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationId)
    {
        var path = context.Request.Path.Value ?? "/";

        // Skip health checks and static assets
        if (SkippedPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");
            var role = context.User.FindFirstValue(ClaimTypes.Role)
                ?? context.User.FindFirstValue("role");

            _logger.LogInformation(
                "{Method} {Path} → {StatusCode} ({ElapsedMs}ms) CorrelationId={CorrelationId} UserId={UserId} Role={Role}",
                context.Request.Method,
                path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                correlationId.CorrelationId ?? "—",
                userId ?? "anonymous",
                role ?? "none");
        }
    }
}
