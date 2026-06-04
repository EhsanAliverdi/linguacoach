using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Diagnostics;

/// <summary>
/// ILoggerProvider that writes filtered log events to the DiagnosticEventBuffer.
/// Only captures Information, Warning, and Error levels.
/// Intentionally excludes Debug/Trace and EF Core SQL query logs.
/// Never captures category names containing sensitive data.
/// </summary>
public sealed class DiagnosticLoggerProvider : ILoggerProvider
{
    private static readonly HashSet<string> SkippedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Database.Command",
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "Microsoft.AspNetCore.StaticFiles",
    };

    private readonly DiagnosticEventBuffer _buffer;

    public DiagnosticLoggerProvider(DiagnosticEventBuffer buffer) => _buffer = buffer;

    public ILogger CreateLogger(string categoryName)
        => new DiagnosticLogger(categoryName, _buffer);

    public void Dispose() { }
}

internal sealed class DiagnosticLogger : ILogger
{
    private readonly string _category;
    private readonly DiagnosticEventBuffer _buffer;

    // Store scope state for extracting CorrelationId
    [ThreadStatic]
    private static Dictionary<string, object>? _scopeState;

    public DiagnosticLogger(string category, DiagnosticEventBuffer buffer)
    {
        _category = category;
        _buffer = buffer;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        if (state is Dictionary<string, object> dict)
        {
            var prev = _scopeState;
            _scopeState = dict;
            return new ScopeDisposable(() => _scopeState = prev);
        }
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= LogLevel.Information && _buffer.IsEnabled;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        // Skip noisy framework categories
        foreach (var skip in new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.Hosting.Diagnostics" })
        {
            if (_category.StartsWith(skip, StringComparison.OrdinalIgnoreCase)) return;
        }

        var correlationId = _scopeState?.TryGetValue("CorrelationId", out var cid) == true
            ? cid?.ToString()
            : null;

        var message = formatter(state, exception);
        if (exception is not null)
            message = $"{message} [{exception.GetType().Name}]";

        _buffer.Add(new DiagnosticEvent(
            TimestampUtc: DateTimeOffset.UtcNow,
            Level: logLevel.ToString(),
            Category: ShortCategory(_category),
            Message: message,
            CorrelationId: correlationId,
            UserId: null,
            Path: null,
            StatusCode: null,
            ElapsedMs: null));
    }

    private static string ShortCategory(string full)
    {
        // Return last two segments for readability: "LinguaCoach.Infrastructure.Activity.ActivityGetHandler" → "Activity.ActivityGetHandler"
        var parts = full.Split('.');
        return parts.Length >= 2
            ? string.Join(".", parts[^Math.Min(2, parts.Length)..])
            : full;
    }

    private sealed class ScopeDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public ScopeDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
