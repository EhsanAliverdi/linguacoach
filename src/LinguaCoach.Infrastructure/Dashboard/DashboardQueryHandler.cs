using LinguaCoach.Application.Dashboard;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Dashboard;

public sealed class DashboardQueryHandler : IDashboardQueryHandler
{
    private const int CompletionThreshold = 3;

    private readonly LinguaCoachDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardQueryHandler(LinguaCoachDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<DashboardResult> HandleAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != OnboardingStatus.Complete)
            throw new InvalidOperationException("Dashboard is only available after onboarding is complete.");

        var user = await _userManager.FindByIdAsync(query.UserId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        var careerName = profile.CareerProfile?.Name ?? "your selected role";
        var pathSummary = await BuildPathSummaryAsync(profile.Id, query.UserId, ct);

        return new DashboardResult(
            StudentName: user.Email!,
            CareerProfileName: careerName,
            CefrLevel: profile.CefrLevel,
            Message: pathSummary is null
                ? "Your personalised learning path is being prepared."
                : $"You are on module {pathSummary.CurrentModule?.Order ?? 1} of {pathSummary.TotalModules}.",
            LearningPath: pathSummary);
    }

    private async Task<DashboardLearningPathSummary?> BuildPathSummaryAsync(
        Guid studentProfileId, Guid userId, CancellationToken ct)
    {
        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

        if (path is null)
            return null;

        var modules = path.Modules.OrderBy(m => m.Order).ToList();
        var moduleIds = modules.Select(m => m.Id).ToList();

        var completedCounts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId)
            .Join(_db.LearningActivities.Where(la => la.LearningModuleId.HasValue && moduleIds.Contains(la.LearningModuleId!.Value)),
                  attempt => attempt.LearningActivityId,
                  activity => activity.Id,
                  (attempt, activity) => activity.LearningModuleId!.Value)
            .GroupBy(moduleId => moduleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct);

        var currentModule = modules
            .FirstOrDefault(m => completedCounts.GetValueOrDefault(m.Id, 0) < CompletionThreshold)
            ?? modules.LastOrDefault();

        int modulesCompleted = modules.Count(m =>
            completedCounts.GetValueOrDefault(m.Id, 0) >= CompletionThreshold);

        DashboardModuleSummary? currentDto = currentModule is null ? null
            : new DashboardModuleSummary(
                ModuleId: currentModule.Id,
                Title: currentModule.Title,
                Description: currentModule.Description,
                Order: currentModule.Order,
                CompletedActivities: completedCounts.GetValueOrDefault(currentModule.Id, 0),
                TotalActivities: CompletionThreshold);

        return new DashboardLearningPathSummary(
            PathId: path.Id,
            Title: path.Title,
            CurrentModule: currentDto,
            ModulesCompleted: modulesCompleted,
            TotalModules: modules.Count);
    }
}
