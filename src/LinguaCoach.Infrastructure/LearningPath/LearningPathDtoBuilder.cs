using LinguaCoach.Application.LearningPath;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.LearningPath;

public sealed class LearningPathDtoBuilder
{
    private const int CompletionThreshold = 3;
    private readonly LinguaCoachDbContext _db;

    public LearningPathDtoBuilder(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<LearningPathDto> BuildAsync(
        Domain.Entities.LearningPath path,
        Guid studentProfileId,
        CancellationToken ct)
    {
        var modules = path.Modules.OrderBy(m => m.Order).ToList();
        var moduleIds = modules.Select(m => m.Id).ToList();

        var activityModulePairs = _db.LearningActivities
            .Where(la => la.LearningModuleId.HasValue && moduleIds.Contains(la.LearningModuleId.Value));

        var completedCounts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId)
            .Join(activityModulePairs,
                attempt => attempt.LearningActivityId,
                activity => activity.Id,
                (attempt, activity) => new { ModuleId = activity.LearningModuleId!.Value, attempt.LearningActivityId })
            .Distinct()
            .GroupBy(x => x.ModuleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct);

        var scores = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId && a.Score.HasValue)
            .Join(activityModulePairs,
                attempt => attempt.LearningActivityId,
                activity => activity.Id,
                (attempt, activity) => new { ModuleId = activity.LearningModuleId!.Value, attempt.Score, attempt.CreatedAt })
            .ToListAsync(ct);

        var currentModule = modules
            .FirstOrDefault(m => completedCounts.GetValueOrDefault(m.Id, 0) < CompletionThreshold)
            ?? modules.LastOrDefault();

        var modulesCompleted = modules.Count(m => m.IsCompleted);

        var moduleDtos = modules.Select(m =>
        {
            var moduleScores = scores.Where(s => s.ModuleId == m.Id).ToList();
            var completed = completedCounts.GetValueOrDefault(m.Id, 0);
            var average = moduleScores.Count == 0 ? null : moduleScores.Average(s => s.Score);
            return new LearningModuleDto(
                ModuleId: m.Id,
                Title: m.Title,
                Description: m.Description,
                Order: m.Order,
                CompletedActivities: completed,
                TotalActivities: CompletionThreshold,
                IsCurrent: currentModule is not null && m.Id == currentModule.Id,
                IsCompleted: m.IsCompleted,
                IsReadyToComplete: completed >= CompletionThreshold && average >= 75,
                AverageScore: average,
                LatestScore: moduleScores.OrderByDescending(s => s.CreatedAt).FirstOrDefault()?.Score,
                FocusSkill: m.FocusSkill,
                Reason: m.Reason,
                Difficulty: m.Difficulty);
        }).ToList();

        return new LearningPathDto(
            PathId: path.Id,
            Title: path.Title,
            IsActive: path.IsActive,
            CurrentModule: currentModule is null ? null : moduleDtos.First(m => m.ModuleId == currentModule.Id),
            ModulesCompleted: modulesCompleted,
            TotalModules: modules.Count,
            Modules: moduleDtos);
    }
}
