using LinguaCoach.Application.History;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.History;

public sealed class ModuleActivitiesHandler : IGetModuleActivitiesHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly StudentProgressService _progress;
    private readonly ILogger<ModuleActivitiesHandler> _logger;

    public ModuleActivitiesHandler(
        LinguaCoachDbContext db,
        StudentProgressService progress,
        ILogger<ModuleActivitiesHandler> logger)
    {
        _db = db;
        _progress = progress;
        _logger = logger;
    }

    public async Task<ModuleActivityHistoryDto> HandleAsync(
        GetModuleActivitiesQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        // Verify module belongs to the student's active path
        var module = await _db.LearningModules
            .Include(m => m.Activities)
            .FirstOrDefaultAsync(m => m.Id == query.ModuleId, ct)
            ?? throw new KeyNotFoundException($"Module {query.ModuleId} not found.");

        var pathBelongsToStudent = await _db.LearningPaths
            .AnyAsync(p => p.Id == module.LearningPathId
                        && p.StudentProfileId == profile.Id
                        && p.IsActive, ct);

        if (!pathBelongsToStudent)
            throw new UnauthorizedAccessException("Module does not belong to the student's active path.");

        // Get progress data
        var progressData = await _progress.GetModuleProgressAsync(
            profile.Id, new[] { module.Id }, ct);
        var pd = progressData.GetValueOrDefault(module.Id);

        // Get all attempts for this student's activities in this module
        var moduleActivityIds = module.Activities.Select(a => a.Id).ToList();
        var allAttempts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == profile.Id
                     && moduleActivityIds.Contains(a.LearningActivityId))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        // Build per-activity summaries (only activities this student has attempted)
        var attemptsByActivity = allAttempts
            .GroupBy(a => a.LearningActivityId)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.CreatedAt).ToList());

        var activitySummaries = new List<ActivitySummaryDto>();
        foreach (var activity in module.Activities.OrderBy(a => a.Id))
        {
            if (!attemptsByActivity.TryGetValue(activity.Id, out var attempts) || attempts.Count == 0)
                continue; // only show activities the student has attempted

            var scores = attempts.Where(a => a.Score.HasValue).Select(a => a.Score!.Value).ToList();
            var hasFeedback = attempts.Any(a => a.FeedbackJson is not null and not "{}" and not "null");

            activitySummaries.Add(new ActivitySummaryDto(
                ActivityId: activity.Id,
                Title: activity.Title,
                ActivityType: activity.ActivityType.ToString(),
                AttemptCount: attempts.Count,
                BestScore: scores.Count > 0 ? Math.Round(scores.Max(), 0) : null,
                LatestScore: scores.Count > 0 ? Math.Round(scores.Last(), 0) : null,
                LatestAttemptAt: attempts.Last().CreatedAt,
                HasFeedback: hasFeedback));
        }

        _logger.LogInformation(
            "Module activity history returned ModuleId={ModuleId} ActivityCount={Count} UserId={UserId}",
            query.ModuleId, activitySummaries.Count, query.UserId);

        const int CompletionThreshold = 3;
        return new ModuleActivityHistoryDto(
            ModuleId: module.Id,
            Title: module.Title,
            Description: module.Description,
            CompletedActivities: pd?.DistinctCompleted ?? 0,
            TotalRequired: CompletionThreshold,
            AverageScore: pd?.AverageScore,
            LatestScore: pd?.LatestScore,
            IsReadyToComplete: pd?.IsReadyToComplete ?? false,
            IsCompleted: module.IsCompleted,
            Activities: activitySummaries);
    }
}
