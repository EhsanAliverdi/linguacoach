using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Handles session and exercise lifecycle commands:
/// start session, complete session, complete exercise.
///
/// Lifecycle stage transitions:
///   CourseReady  → InLesson       (on session start)
///   InLesson     → ActiveLearning (on session complete)
/// </summary>
public sealed class SessionLifecycleHandler :
    IStartSessionHandler,
    ICompleteSessionHandler,
    ICompleteExerciseHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<SessionLifecycleHandler> _logger;

    public SessionLifecycleHandler(
        LinguaCoachDbContext db,
        ILogger<SessionLifecycleHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Start session ──────────────────────────────────────────────────────────

    public async Task<StartSessionResult> HandleAsync(StartSessionCommand command, CancellationToken ct = default)
    {
        var (profile, session) = await LoadAndVerifyAsync(command.UserId, command.SessionId, ct);

        if (session.Status == SessionStatus.InProgress)
        {
            // Idempotent: already started — return current state.
            return new StartSessionResult(session.Id, session.Status, session.StartedAtUtc!.Value);
        }

        session.Start();

        // CourseReady → InLesson
        if (profile.LifecycleStage == StudentLifecycleStage.CourseReady)
        {
            profile.SetLifecycleStage(StudentLifecycleStage.InLesson);
            _logger.LogInformation(
                "Lifecycle: student {ProfileId} CourseReady → InLesson (session {SessionId})",
                profile.Id, session.Id);
        }

        await _db.SaveChangesAsync(ct);

        return new StartSessionResult(session.Id, session.Status, session.StartedAtUtc!.Value);
    }

    // ── Complete session ───────────────────────────────────────────────────────

    public async Task<CompleteSessionResult> HandleAsync(CompleteSessionCommand command, CancellationToken ct = default)
    {
        var (profile, session) = await LoadAndVerifyAsync(command.UserId, command.SessionId, ct);

        if (session.Status == SessionStatus.Completed)
        {
            // Idempotent: already completed — return current state.
            return new CompleteSessionResult(session.Id, session.Status, session.CompletedAtUtc!.Value);
        }

        // Auto-start if not yet started (student skipped the start call).
        if (session.Status == SessionStatus.NotStarted)
        {
            session.Start();
            if (profile.LifecycleStage == StudentLifecycleStage.CourseReady)
                profile.SetLifecycleStage(StudentLifecycleStage.InLesson);
        }

        session.Complete();

        // InLesson → ActiveLearning
        if (profile.LifecycleStage == StudentLifecycleStage.InLesson)
        {
            profile.SetLifecycleStage(StudentLifecycleStage.ActiveLearning);
            _logger.LogInformation(
                "Lifecycle: student {ProfileId} InLesson → ActiveLearning (session {SessionId})",
                profile.Id, session.Id);
        }

        await _db.SaveChangesAsync(ct);

        // Phase I2B — the legacy lesson-buffer refill trigger (LessonBatchGenerationJob) was
        // deleted along with the rest of the legacy generation pipeline. Today now re-selects a
        // bank-first Today Plan Module live on next page load (ITodayPlanModuleSelectionService
        // via SessionQueryHandler) — no background pre-generation to trigger here anymore.

        return new CompleteSessionResult(session.Id, session.Status, session.CompletedAtUtc!.Value);
    }

    // ── Complete exercise ──────────────────────────────────────────────────────

    public async Task<CompleteExerciseResult> HandleAsync(CompleteExerciseCommand command, CancellationToken ct = default)
    {
        var (_, session) = await LoadAndVerifyAsync(command.UserId, command.SessionId, ct);

        var exercise = await _db.SessionExercises
            .FirstOrDefaultAsync(e => e.Id == command.ExerciseId && e.LearningSessionId == command.SessionId, ct)
            ?? throw new InvalidOperationException(
                $"Exercise {command.ExerciseId} not found in session {command.SessionId}.");

        if (exercise.Status == ExerciseStatus.Completed)
        {
            // Idempotent.
            return new CompleteExerciseResult(exercise.Id, exercise.Status, exercise.CompletedAtUtc!.Value,
                SessionComplete: session.Status == SessionStatus.Completed);
        }

        exercise.Complete();

        // Auto-start the session when the first exercise is completed, if not already started.
        if (session.Status == SessionStatus.NotStarted)
            session.Start();

        await _db.SaveChangesAsync(ct);

        // Check whether all exercises are now complete.
        var allComplete = await _db.SessionExercises
            .Where(e => e.LearningSessionId == command.SessionId)
            .AllAsync(e => e.Status == ExerciseStatus.Completed || e.Status == ExerciseStatus.Skipped, ct);

        return new CompleteExerciseResult(exercise.Id, exercise.Status, exercise.CompletedAtUtc!.Value,
            SessionComplete: allComplete);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<(Domain.Entities.StudentProfile profile, Domain.Entities.LearningSession session)>
        LoadAndVerifyAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var session = await _db.LearningSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        // Ownership check: session's module must be on the student's active path.
        var moduleOnStudentPath = await _db.LearningPaths
            .Where(p => p.StudentProfileId == profile.Id && p.IsActive)
            .SelectMany(p => p.Modules)
            .AnyAsync(m => m.Id == session.LearningModuleId, ct);

        if (!moduleOnStudentPath)
            throw new UnauthorizedAccessException("Session does not belong to this student.");

        return (profile, session);
    }
}
