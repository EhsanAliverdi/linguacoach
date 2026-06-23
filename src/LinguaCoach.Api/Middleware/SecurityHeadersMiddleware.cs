namespace LinguaCoach.Api.Middleware;

/// <summary>
/// Adds baseline security headers to every API response.
/// CSP and HSTS are deliberately omitted here:
///   - CSP requires a dedicated pass aligned with the Angular build/nonce strategy.
///   - HSTS should only be enabled after confirming the production reverse-proxy terminates TLS correctly.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
