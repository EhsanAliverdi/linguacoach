using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Sessions;

// ── Request ────────────────────────────────────────────────────────────────────

/// <summary>
/// Request to get or create today's LearningSession for a student.
/// Returns the existing InProgress or NotStarted session for today if one exists,
/// otherwise generates a new one from the student's profile and active module.
/// </summary>
public sealed record GetOrCreateTodaysSessionCommand(Guid StudentProfileId);

// ── Result ─────────────────────────────────────────────────────────────────────

/// <summary>Summary of the session returned to callers.</summary>
public sealed record TodaysSessionResult(
    Guid SessionId,
    string Title,
    string Topic,
    string SessionGoal,
    int DurationMinutes,
    string FocusSkill,
    SessionStatus Status,
    bool IsResuming,
    IReadOnlyList<SessionExerciseResult> Exercises);

/// <summary>Ordered step within the session.</summary>
public sealed record SessionExerciseResult(
    Guid ExerciseId,
    int Order,
    ExerciseKind Kind,
    string ExercisePatternKey,
    string PrimarySkill,
    string Instructions,
    int EstimatedMinutes,
    ExerciseStatus Status,
    Guid? LearningActivityId);

// ── Service interface ──────────────────────────────────────────────────────────

public interface ISessionGeneratorService
{
    /// <summary>
    /// Returns today's session for the student, generating it if necessary.
    /// Idempotent: calling twice for the same student on the same day returns the same session.
    /// </summary>
    Task<TodaysSessionResult> GetOrCreateTodaysSessionAsync(
        GetOrCreateTodaysSessionCommand command,
        CancellationToken ct = default);
}
