using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Sessions;

// ── Today's session ────────────────────────────────────────────────────────────

public sealed record GetTodaysSessionQuery(Guid UserId);

public interface IGetTodaysSessionHandler
{
    Task<TodaysSessionResult> HandleAsync(GetTodaysSessionQuery query, CancellationToken ct = default);
}

// ── Get session by id ──────────────────────────────────────────────────────────

public sealed record GetSessionQuery(Guid UserId, Guid SessionId);

public sealed record SessionDetailResult(
    Guid SessionId,
    string Title,
    string Topic,
    string SessionGoal,
    int DurationMinutes,
    string FocusSkill,
    SessionStatus Status,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<SessionExerciseResult> Exercises);

public interface IGetSessionHandler
{
    Task<SessionDetailResult> HandleAsync(GetSessionQuery query, CancellationToken ct = default);
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

// ── Prepare exercise (generate / attach LearningActivity) ─────────────────────

public sealed record PrepareExerciseCommand(Guid UserId, Guid SessionId, Guid ExerciseId);

/// <param name="ActivityId">The generated or pre-existing LearningActivity id.</param>
/// <param name="ActivityType">Resolved ActivityType for the exercise kind.</param>
/// <param name="IsReview">True when the exercise is a Review step (no full activity generated).</param>
public sealed record PrepareExerciseResult(
    Guid ActivityId,
    Domain.Enums.ActivityType? ActivityType,
    bool IsReview);

public interface IPrepareExerciseHandler
{
    Task<PrepareExerciseResult> HandleAsync(PrepareExerciseCommand command, CancellationToken ct = default);
}
