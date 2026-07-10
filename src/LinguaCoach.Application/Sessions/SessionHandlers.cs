using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Sessions;

// ── Today's session ────────────────────────────────────────────────────────────

public sealed record GetTodaysSessionQuery(Guid UserId);

public interface IGetTodaysSessionHandler
{
    Task<TodaysSessionResult> HandleAsync(GetTodaysSessionQuery query, CancellationToken ct = default);
}

// ── Start session ──────────────────────────────────────────────────────────────

public sealed record StartSessionCommand(Guid UserId, Guid SessionId);

public sealed record StartSessionResult(Guid SessionId, SessionStatus Status, DateTime StartedAtUtc);

public interface IStartSessionHandler
{
    Task<StartSessionResult> HandleAsync(StartSessionCommand command, CancellationToken ct = default);
}

// ── Complete session ───────────────────────────────────────────────────────────

public sealed record CompleteSessionCommand(Guid UserId, Guid SessionId);

public sealed record CompleteSessionResult(Guid SessionId, SessionStatus Status, DateTime CompletedAtUtc);

public interface ICompleteSessionHandler
{
    Task<CompleteSessionResult> HandleAsync(CompleteSessionCommand command, CancellationToken ct = default);
}

// ── Complete exercise ──────────────────────────────────────────────────────────

public sealed record CompleteExerciseCommand(Guid UserId, Guid SessionId, Guid ExerciseId);

public sealed record CompleteExerciseResult(
    Guid ExerciseId,
    ExerciseStatus Status,
    DateTime CompletedAtUtc,
    bool SessionComplete);

public interface ICompleteExerciseHandler
{
    Task<CompleteExerciseResult> HandleAsync(CompleteExerciseCommand command, CancellationToken ct = default);
}

// ── Session history ────────────────────────────────────────────────────────────

public sealed record GetSessionHistoryQuery(Guid UserId, int Page = 1, int PageSize = 20);

public sealed record SessionHistoryExerciseResult(
    Guid ExerciseId,
    int Order,
    string ExercisePatternKey,
    string PrimarySkill,
    ExerciseStatus Status,
    double? Score,
    DateTime? CompletedAtUtc);

public sealed record SessionHistoryItem(
    Guid SessionId,
    string Title,
    string Topic,
    string FocusSkill,
    SessionStatus Status,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<SessionHistoryExerciseResult> Exercises);

public sealed record SessionHistoryResult(
    IReadOnlyList<SessionHistoryItem> Sessions,
    int TotalCount,
    int Page,
    int PageSize);

public interface IGetSessionHistoryHandler
{
    Task<SessionHistoryResult> HandleAsync(GetSessionHistoryQuery query, CancellationToken ct = default);
}

// ── Prepare exercise / get-session-by-id: removed in Phase I2B ────────────────
// IPrepareExerciseHandler (ExercisePrepareHandler) and IGetSessionHandler (GetSessionQuery /
// SessionDetailResult) were deleted along with the legacy generation pipeline and its
// lesson-runner UI. Today is module-only now; nothing creates new SessionExercise rows that would
// need on-open activity preparation, and GET /api/sessions/{id} had zero remaining frontend
// callers once the lesson-runner page was removed. See
// docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md.
