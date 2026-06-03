using LinguaCoach.Application.LearningPath;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.LearningPath;

public sealed class LearningPathQueryHandler : IGetLearningPathHandler
{
    private const int CompletionThreshold = 3;

    private readonly LinguaCoachDbContext _db;

    public LearningPathQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LearningPathDto?> HandleAsync(GetLearningPathQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == profile.Id && p.IsActive, ct);

        if (path is null)
            return null;

        var modules = path.Modules.OrderBy(m => m.Order).ToList();
        var moduleIds = modules.Select(m => m.Id).ToList();

        var completedCounts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == profile.Id)
            .Join(_db.LearningActivities.Where(la => la.LearningModuleId.HasValue && moduleIds.Contains(la.LearningModuleId!.Value)),
                  attempt => attempt.LearningActivityId,
                  activity => activity.Id,
                  (attempt, activity) => activity.LearningModuleId!.Value)
            .GroupBy(moduleId => moduleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct);

        Domain.Entities.LearningModule? currentModule = modules
            .FirstOrDefault(m => completedCounts.GetValueOrDefault(m.Id, 0) < CompletionThreshold)
            ?? modules.LastOrDefault();

        int modulesCompleted = modules.Count(m =>
            completedCounts.GetValueOrDefault(m.Id, 0) >= CompletionThreshold);

        var moduleDtos = modules.Select(m =>
        {
            int completed = completedCounts.GetValueOrDefault(m.Id, 0);
            return new LearningModuleDto(
                ModuleId: m.Id,
                Title: m.Title,
                Description: m.Description,
                Order: m.Order,
                CompletedActivities: completed,
                TotalActivities: CompletionThreshold,
                IsCurrent: currentModule is not null && m.Id == currentModule.Id);
        }).ToList();

        var currentDto = currentModule is null ? null
            : moduleDtos.First(m => m.ModuleId == currentModule.Id);

        return new LearningPathDto(
            PathId: path.Id,
            Title: path.Title,
            IsActive: path.IsActive,
            CurrentModule: currentDto,
            ModulesCompleted: modulesCompleted,
            TotalModules: modules.Count,
            Modules: moduleDtos);
    }
}
