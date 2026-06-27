using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Handles read-only session queries: today's session and session detail.
/// </summary>
public sealed class SessionQueryHandler : IGetTodaysSessionHandler, IGetSessionHandler, IGetSessionHistoryHandler
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
            CefrLevel: profile.CefrLevel,
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

    public async Task<SessionHistoryResult> HandleAsync(GetSessionHistoryQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        // All module IDs on the student's learning paths
        var moduleIds = await _db.LearningPaths
            .Where(p => p.StudentProfileId == profile.Id)
            .SelectMany(p => p.Modules)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var sessionQuery = _db.LearningSessions.Where(s => moduleIds.Contains(s.LearningModuleId));
        var totalCount = await sessionQuery.CountAsync(ct);

        var sessions = await sessionQuery
            .OrderByDescending(s => s.StartedAtUtc ?? s.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var sessionIds = sessions.Select(s => s.Id).ToList();

        // Fetch exercises + best scores for each in one query
        var exercises = await _db.SessionExercises
            .Where(e => sessionIds.Contains(e.LearningSessionId))
            .OrderBy(e => e.LearningSessionId)
            .ThenBy(e => e.Order)
            .ToListAsync(ct);

        var activityIds = exercises
            .Where(e => e.LearningActivityId.HasValue)
            .Select(e => e.LearningActivityId!.Value)
            .ToList();

        // Best score per activity (max score across all attempts)
        var scoresByActivity = await _db.ActivityAttempts
            .Where(a => activityIds.Contains(a.LearningActivityId))
            .GroupBy(a => a.LearningActivityId)
            .Select(g => new { ActivityId = g.Key, BestScore = g.Max(a => a.Score) })
            .ToDictionaryAsync(x => x.ActivityId, x => x.BestScore, ct);

        var exercisesBySession = exercises.ToLookup(e => e.LearningSessionId);

        var items = sessions.Select(s =>
        {
            var exs = exercisesBySession[s.Id].Select(e => new SessionHistoryExerciseResult(
                ExerciseId: e.Id,
                Order: e.Order,
                ExercisePatternKey: e.ExercisePatternKey,
                PrimarySkill: e.PrimarySkill,
                Status: e.Status,
                Score: e.LearningActivityId.HasValue && scoresByActivity.TryGetValue(e.LearningActivityId.Value, out var sc) ? sc : null,
                CompletedAtUtc: e.CompletedAtUtc)).ToList();

            return new SessionHistoryItem(
                SessionId: s.Id,
                Title: s.Title,
                Topic: s.Topic,
                FocusSkill: s.FocusSkill,
                Status: s.Status,
                StartedAtUtc: s.StartedAtUtc,
                CompletedAtUtc: s.CompletedAtUtc,
                Exercises: exs);
        }).ToList();

        return new SessionHistoryResult(items, totalCount, query.Page, query.PageSize);
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
        ExercisePatternKey.PhraseMatch
            or ExercisePatternKey.GapFillWorkplacePhrase => ExerciseKind.VocabularyWarmup,
        ExercisePatternKey.ListenAndAnswer
            or ExercisePatternKey.ListenAndGapFill => ExerciseKind.ListeningInput,
        ExercisePatternKey.EmailReply
            or ExercisePatternKey.TeamsChatSimulation
            or "writing_response" => ExerciseKind.WritingTask,
        ExercisePatternKey.SpokenResponseFromPrompt
            or "speaking_role_play" => ExerciseKind.SpeakingTask,
        ExercisePatternKey.LessonReflection => ExerciseKind.Review,
        _ when patternKey.StartsWith("listen", StringComparison.OrdinalIgnoreCase) => ExerciseKind.ListeningInput,
        _ when patternKey.StartsWith("speaking", StringComparison.OrdinalIgnoreCase) => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("writing", StringComparison.OrdinalIgnoreCase) => ExerciseKind.WritingTask,
        _ => ExerciseKind.ContextInput
    };
}
