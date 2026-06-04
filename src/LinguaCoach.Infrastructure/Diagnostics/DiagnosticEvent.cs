namespace LinguaCoach.Infrastructure.Diagnostics;

public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string Level,
    string Category,
    string Message,
    string? CorrelationId,
    string? UserId,
    string? Path,
    int? StatusCode,
    long? ElapsedMs);
