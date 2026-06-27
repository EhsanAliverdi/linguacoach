using LinguaCoach.Application.Dashboard;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Progress;
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
    private readonly StudentProgressService _progress;

    public DashboardQueryHandler(
        LinguaCoachDbContext db,
        UserManager<ApplicationUser> userManager,
        StudentProgressService progress)
    {
        _db = db;
        _userManager = userManager;
        _progress = progress;
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

        var lifecycleStage = profile.LifecycleStage.ToString();

        var careerName = profile.CareerContext ?? profile.CareerProfile?.Name ?? "your selected role";
        var pathSummary = await BuildPathSummaryAsync(profile.Id, ct);
        var activityStats = await BuildActivityStatsAsync(profile.Id, ct);
        var streakDays = await BuildStreakDaysAsync(profile.Id, ct);
        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);

        DashboardFocusArea? dashFocus = focusArea is null ? null
            : new DashboardFocusArea(focusArea.Category, focusArea.FriendlyLabel);

        string? nextRecommended = BuildNextRecommendedPractice(pathSummary, focusArea);
        string? latestImprovement = BuildLatestImprovement(activityStats, profile.Id);

        string message = profile.LifecycleStage switch
        {
            StudentLifecycleStage.PlacementRequired =>
                "Complete your placement assessment to unlock your personalised course.",
            StudentLifecycleStage.PlacementInProgress =>
                "Continue your placement assessment to unlock your personalised course.",
            // Phase 14B — honest preparing state when plan regen failed or is pending.
            StudentLifecycleStage.PlacementCompleted =>
                "Your personalised course is being prepared. Practice Gym is available while you wait.",
            _ when pathSummary is null =>
                "Your personalised learning path is being prepared.",
            _ =>
                $"You are on module {pathSummary.CurrentModule?.Order ?? 1} of {pathSummary.TotalModules}."
        };

        return new DashboardResult(
            StudentName: user.Email!,
            CareerProfileName: careerName,
            CefrLevel: profile.CefrLevel,
            Message: message,
            LifecycleStage: lifecycleStage,
            LearningPath: pathSummary,
            ActivityStats: activityStats,
            CurrentFocus: dashFocus,
            NextRecommendedPractice: nextRecommended,
            LatestImprovement: latestImprovement,
            StreakDays: streakDays);
    }

    private static string? BuildNextRecommendedPractice(
        DashboardLearningPathSummary? path,
        Application.LearningPath.LearningFocusAreaDto? focus)
    {
        if (path?.CurrentModule is null) return null;

        var mod = path.CurrentModule;
        int remaining = mod.TotalActivities - mod.CompletedActivities;

        if (mod.IsReadyToComplete)
            return "Your module is ready to complete. Open My Path to advance to the next module.";

        if (focus is not null)
        {
            if (remaining <= 0)
                return $"Keep practising {focus.FriendlyLabel} — try another activity to solidify your progress.";
            return $"Focus on {focus.FriendlyLabel}. Complete {remaining} more {(remaining == 1 ? "activity" : "activities")} to finish this module.";
        }

        if (remaining <= 0)
            return "Keep practising — your score is building up.";
        return $"Complete {remaining} more {(remaining == 1 ? "activity" : "activities")} to finish this module.";
    }

    private static string? BuildLatestImprovement(DashboardActivityStats? stats, Guid profileId)
    {
        // Simple message based on average vs latest score
        if (stats?.LatestScore is null || stats.AverageScore is null) return null;
        if (stats.ActivitiesCompleted < 2) return null;

        var diff = Math.Round(stats.LatestScore.Value - stats.AverageScore.Value, 0);
        if (diff > 5) return $"Your latest score ({stats.LatestScore}) is above your average ({stats.AverageScore}) — you're improving!";
        if (diff < -5) return $"Your latest score ({stats.LatestScore}) is below your average ({stats.AverageScore}) — keep practising.";
        return $"Your scores are consistent. Average: {stats.AverageScore}. Keep going!";
    }

    private async Task<DashboardActivityStats> BuildActivityStatsAsync(Guid studentProfileId, CancellationToken ct)
    {
        var attempts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.Score)
            .ToListAsync(ct);

        if (attempts.Count == 0)
            return new DashboardActivityStats(0, null, null);

        var scored = attempts.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        return new DashboardActivityStats(
            ActivitiesCompleted: attempts.Count,
            LatestScore: scored.Count > 0 ? Math.Round(scored.First(), 0) : null,
            AverageScore: scored.Count > 0 ? Math.Round(scored.Average(), 0) : null);
    }

    private async Task<int> BuildStreakDaysAsync(Guid studentProfileId, CancellationToken ct)
    {
        var activityDates = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId)
            .Select(a => a.CreatedAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(ct);

        if (activityDates.Count == 0) return 0;

        var today = DateTime.UtcNow.Date;
        if (activityDates[0] != today && activityDates[0] != today.AddDays(-1))
            return 0;

        var streak = 1;
        var expected = activityDates[0];
        for (int i = 1; i < activityDates.Count; i++)
        {
            expected = expected.AddDays(-1);
            if (activityDates[i] != expected) break;
            streak++;
        }

        return streak;
    }

    private async Task<DashboardLearningPathSummary?> BuildPathSummaryAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

        if (path is null)
            return null;

        var modules = path.Modules.OrderBy(m => m.Order).ToList();
        var moduleIds = modules.Select(m => m.Id).ToList();

        var progressByModule = await _progress.GetModuleProgressAsync(studentProfileId, moduleIds, ct);

        var currentModule = modules.FirstOrDefault(m =>
        {
            if (m.IsCompleted) return false;
            var pd = progressByModule.GetValueOrDefault(m.Id);
            return pd is null || pd.DistinctCompleted < CompletionThreshold;
        }) ?? modules.LastOrDefault();

        int modulesCompleted = modules.Count(m =>
        {
            if (m.IsCompleted) return true;
            var pd = progressByModule.GetValueOrDefault(m.Id);
            return pd is not null && pd.DistinctCompleted >= CompletionThreshold;
        });

        DashboardModuleSummary? currentDto = null;
        if (currentModule is not null)
        {
            var pd = progressByModule.GetValueOrDefault(currentModule.Id);
            currentDto = new DashboardModuleSummary(
                ModuleId: currentModule.Id,
                Title: currentModule.Title,
                Description: currentModule.Description,
                Order: currentModule.Order,
                CompletedActivities: pd?.DistinctCompleted ?? 0,
                TotalActivities: CompletionThreshold,
                IsReadyToComplete: pd?.IsReadyToComplete ?? false,
                AverageScore: pd?.AverageScore);
        }

        return new DashboardLearningPathSummary(
            PathId: path.Id,
            Title: path.Title,
            CurrentModule: currentDto,
            ModulesCompleted: modulesCompleted,
            TotalModules: modules.Count);
    }
}
