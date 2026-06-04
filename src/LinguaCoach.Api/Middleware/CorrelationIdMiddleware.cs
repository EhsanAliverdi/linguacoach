namespace LinguaCoach.Api.Middleware;

/// <summary>
/// Reads or generates a correlation ID for every request.
/// Sets X-Correlation-ID on the response and adds it to the logging scope
/// so all request-scoped log events carry the same ID.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationId)
    {
        var id = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N")[..12]; // short 12-char ID

        correlationId.CorrelationId = id;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (correlationId.Logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = id,
               }))
        {
            await _next(context);
        }
    }
}

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; set; }
    ILogger Logger { get; }
}

/// <summary>Scoped service — one instance per request, carries the correlation ID.</summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    public string? CorrelationId { get; set; }
    public ILogger Logger { get; }

    public CorrelationIdAccessor(ILogger<CorrelationIdAccessor> logger) => Logger = logger;
}
