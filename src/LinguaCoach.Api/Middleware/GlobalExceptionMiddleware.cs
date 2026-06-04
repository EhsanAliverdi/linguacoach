using System.Security.Claims;

namespace LinguaCoach.Api.Middleware;

/// <summary>
/// Catches all unhandled exceptions, logs them once with structured fields,
/// and returns a safe JSON response that never leaks stack traces.
/// Includes correlationId in the response so the user can report it.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationId)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");
            var cid = correlationId.CorrelationId ?? "unknown";

            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}. CorrelationId={CorrelationId} UserId={UserId} ExceptionType={ExceptionType}",
                context.Request.Method,
                context.Request.Path,
                cid,
                userId ?? "anonymous",
                ex.GetType().Name);

            if (context.Response.HasStarted) return;

            context.Response.StatusCode = ex is KeyNotFoundException ? 404
                : ex is UnauthorizedAccessException ? 403
                : ex is ArgumentException or InvalidOperationException ? 400
                : 500;

            context.Response.ContentType = "application/json";

            // Only include detail in Development — never in Production
            var detail = _env.IsDevelopment() ? ex.Message : null;

            await context.Response.WriteAsJsonAsync(new
            {
                message = "Something went wrong. Please try again.",
                correlationId = cid,
                detail,
            });
        }
    }
}
