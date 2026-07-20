using LinguaCoach.Application.TodayPlanModules;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Handles read-only session queries: Today (module-only, Phase I2B) and session history.
///
/// Phase I2B — the legacy LearningSession/SessionExercise generation pipeline
/// (ISessionGeneratorService/SessionGeneratorService) and the on-open activity preparation
/// pipeline (IPrepareExerciseHandler/ExercisePrepareHandler) were deleted. Today now ONLY calls
/// ITodayPlanModuleSelectionService — when it has nothing for the student, Today honestly
/// reports "nothing available" (TodaysSessionResult.Available = false) rather than falling back to
/// any AI generation. See docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md.
/// </summary>
public sealed class SessionQueryHandler : IGetTodaysSessionHandler, IGetSessionHistoryHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly ITodayPlanModuleSelectionService _moduleSelector;
    private readonly ITodayPlanModuleAssignmentRecorder _moduleAssignmentRecorder;
    private readonly ILearningPlanService _learningPlan;
    private readonly ILogger<SessionQueryHandler> _logger;

    public SessionQueryHandler(
        LinguaCoachDbContext db,
        ITodayPlanModuleSelectionService moduleSelector,
        ITodayPlanModuleAssignmentRecorder moduleAssignmentRecorder,
        ILearningPlanService learningPlan,
        ILogger<SessionQueryHandler> logger)
    {
        _db = db;
        _moduleSelector = moduleSelector;
        _moduleAssignmentRecorder = moduleAssignmentRecorder;
        _learningPlan = learningPlan;
        _logger = logger;
    }

    public async Task<TodaysSessionResult> HandleAsync(GetTodaysSessionQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        TodayPlanModuleSelectionResult? todayPlan = null;
        try
        {
            // Soft preference only — the composer treats RequestedSkill as one ranking input
            // among several (weakness/goal matches, CEFR fit), never a hard filter, so a null or
            // stale value here degrades to the prior skill-agnostic behavior rather than starving
            // the plan of candidates.
            string? requestedSkill = null;
            try
            {
                var plannedObjective = await _learningPlan.GetNextPlannedObjectiveAsync(profile.Id, ct: ct);
                requestedSkill = plannedObjective?.PrimarySkill;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not resolve next planned objective for student {StudentProfileId}; Today selection proceeds without a requested skill.",
                    profile.Id);
            }

            todayPlan = await _moduleSelector.SelectAsync(
                new TodayPlanModuleSelectionRequest(
                    StudentId: profile.Id,
                    CefrLevel: profile.CefrLevel,
                    LearningPlanId: null,
                    TargetDate: DateTime.UtcNow.Date,
                    RequestedSkill: requestedSkill),
                ct);

            await _moduleAssignmentRecorder.RecordAsync(profile.Id, DateTime.UtcNow.Date, todayPlan, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Today Plan module selection failed for student {StudentProfileId}; Today has nothing available.", profile.Id);
        }

        var available = todayPlan is { FallbackRequired: false } && todayPlan.SelectedModules.Count > 0;
        return new TodaysSessionResult(available, todayPlan);
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
}
