using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Handles read-only session queries: today's session and session detail.
/// </summary>
public sealed class SessionQueryHandler : IGetTodaysSessionHandler, IGetSessionHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly ISessionGeneratorService _generator;

    public SessionQueryHandler(LinguaCoachDbContext db, ISessionGeneratorService generator)
    {
        _db = db;
        _generator = generator;
    }

    public async Task<TodaysSessionResult> HandleAsync(GetTodaysSessionQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        return await _generator.GetOrCreateTodaysSessionAsync(
            new GetOrCreateTodaysSessionCommand(profile.Id), ct);
    }

    public async Task<SessionDetailResult> HandleAsync(GetSessionQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var session = await _db.LearningSessions
            .FirstOrDefaultAsync(s => s.Id == query.SessionId, ct)
            ?? throw new InvalidOperationException($"Session {query.SessionId} not found.");

        // Ownership: the session must belong to a module on the student's active path.
        await EnsureSessionBelongsToStudentAsync(session.LearningModuleId, profile.Id, ct);

        var exercises = await _db.SessionExercises
            .Where(e => e.LearningSessionId == session.Id)
            .OrderBy(e => e.Order)
            .ToListAsync(ct);

        return new SessionDetailResult(
            SessionId: session.Id,
            Title: session.Title,
            Topic: session.Topic,
            SessionGoal: session.SessionGoal,
            DurationMinutes: session.DurationMinutes,
            FocusSkill: session.FocusSkill,
            Status: session.Status,
            StartedAtUtc: session.StartedAtUtc,
            CompletedAtUtc: session.CompletedAtUtc,
            Exercises: exercises.Select(e => new SessionExerciseResult(
                ExerciseId: e.Id,
                Order: e.Order,
                Kind: ResolveKind(e.ExercisePatternKey),
                ExercisePatternKey: e.ExercisePatternKey,
                PrimarySkill: e.PrimarySkill,
                Instructions: e.Instructions,
                EstimatedMinutes: e.EstimatedMinutes,
                Status: e.Status,
                LearningActivityId: e.LearningActivityId)).ToList());
    }

    private async Task EnsureSessionBelongsToStudentAsync(
        Guid moduleId, Guid studentProfileId, CancellationToken ct)
    {
        var moduleOnStudentPath = await _db.LearningPaths
            .Where(p => p.StudentProfileId == studentProfileId && p.IsActive)
            .SelectMany(p => p.Modules)
            .AnyAsync(m => m.Id == moduleId, ct);

        if (!moduleOnStudentPath)
            throw new UnauthorizedAccessException("Session does not belong to this student.");
    }

    private static ExerciseKind ResolveKind(string patternKey) => patternKey switch
    {
        "phrase_match" => ExerciseKind.VocabularyWarmup,
        "listen_and_answer" or "listen_and_gap_fill" => ExerciseKind.ListeningInput,
        "writing_response" => ExerciseKind.WritingTask,
        "speaking_role_play" => ExerciseKind.SpeakingTask,
        "lesson_reflection" => ExerciseKind.Review,
        _ when patternKey.StartsWith("listen") => ExerciseKind.ListeningInput,
        _ when patternKey.StartsWith("speaking") => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("writing") => ExerciseKind.WritingTask,
        _ => ExerciseKind.ContextInput
    };
}
