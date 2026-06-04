using LinguaCoach.Application.LearningPath;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.LearningPath;

public sealed class LearningPathQueryHandler : IGetLearningPathHandler
{
    private const int CompletionThreshold = 3;

    private readonly LinguaCoachDbContext _db;
    private readonly StudentProgressService _progress;

    public LearningPathQueryHandler(LinguaCoachDbContext db, StudentProgressService progress)
    {
        _db = db;
        _progress = progress;
    }

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

        var progressByModule = await _progress.GetModuleProgressAsync(profile.Id, moduleIds, ct);
        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);

        // Current module = first non-completed module (by explicit completion or by threshold)
        Domain.Entities.LearningModule? currentModule = modules.FirstOrDefault(m =>
        {
            if (m.IsCompleted) return false;
            var p = progressByModule.GetValueOrDefault(m.Id);
            return p is null || p.DistinctCompleted < CompletionThreshold;
        }) ?? modules.LastOrDefault();

        int modulesCompleted = modules.Count(m =>
        {
            if (m.IsCompleted) return true;
            var p = progressByModule.GetValueOrDefault(m.Id);
            return p is not null && p.DistinctCompleted >= CompletionThreshold;
        });

        var moduleDtos = modules.Select(m =>
        {
            var pd = progressByModule.GetValueOrDefault(m.Id);
            bool isComplete = m.IsCompleted
                || (pd is not null && pd.DistinctCompleted >= CompletionThreshold);

            return new LearningModuleDto(
                ModuleId: m.Id,
                Title: m.Title,
                Description: m.Description,
                Order: m.Order,
                CompletedActivities: pd?.DistinctCompleted ?? 0,
                TotalActivities: CompletionThreshold,
                IsCurrent: currentModule is not null && m.Id == currentModule.Id,
                IsCompleted: isComplete,
                IsReadyToComplete: pd?.IsReadyToComplete ?? false,
                AverageScore: pd?.AverageScore,
                LatestScore: pd?.LatestScore);
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
            Modules: moduleDtos,
            CurrentFocus: focusArea);
    }
}
