using LinguaCoach.Application.Progress;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Progress;

public sealed class GetProgressHandler : IGetProgressHandler
{
    private const int ScoreTrendLimit = 10;
    private const int TopSkillCount = 3;
    private const int CompletionThreshold = 3;

    private readonly LinguaCoachDbContext _db;
    private readonly StudentProgressService _progressService;

    public GetProgressHandler(LinguaCoachDbContext db, StudentProgressService progressService)
    {
        _db = db;
        _progressService = progressService;
    }

    public async Task<ProgressSummaryDto> HandleAsync(GetProgressQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != OnboardingStatus.Complete)
            throw new InvalidOperationException("Progress is only available after onboarding is complete.");

        var studentProfileId = profile.Id;

        var stats = await BuildStatsAsync(studentProfileId, ct);
        var scoreTrend = await BuildScoreTrendAsync(studentProfileId, ct);
        var skillProgress = await BuildSkillProgressAsync(studentProfileId, ct);
        var learningFocus = await BuildLearningFocusAsync(studentProfileId, ct);
        var moduleProgress = await BuildModuleProgressAsync(studentProfileId, ct);

        return new ProgressSummaryDto(
            Stats: stats,
            ScoreTrend: scoreTrend,
            SkillProgress: skillProgress,
            LearningFocus: learningFocus,
            ModuleProgress: moduleProgress);
    }

    private async Task<ProgressStatsDto> BuildStatsAsync(Guid studentProfileId, CancellationToken ct)
    {
        var attempts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.LearningActivityId, a.Score, a.CreatedAt })
            .ToListAsync(ct);

        if (attempts.Count == 0)
        {
            var emptyModuleProgress = await BuildCurrentModuleProgressAsync(studentProfileId, ct);
            return new ProgressStatsDto(0, 0, 0, null, null, null, 0, 0, emptyModuleProgress);
        }

        int totalAttempts = attempts.Count;
        int activitiesCompleted = attempts.Select(a => a.LearningActivityId).Distinct().Count();
        int retryAttempts = totalAttempts - activitiesCompleted;

        var scored = attempts.Where(a => a.Score.HasValue).Select(a => a.Score!.Value).ToList();
        double? averageScore = scored.Count > 0 ? Math.Round(scored.Average(), 0) : null;
        double? latestScore = scored.Count > 0 ? Math.Round(scored.First(), 0) : null;
        double? bestScore = scored.Count > 0 ? Math.Round(scored.Max(), 0) : null;

        var weekStart = GetWeekStart(DateTime.UtcNow);
        int activitiesThisWeek = attempts
            .Where(a => a.CreatedAt >= weekStart)
            .Select(a => a.LearningActivityId)
            .Distinct()
            .Count();

        int modulesCompleted = await CountModulesCompletedAsync(studentProfileId, ct);
        var currentModule = await BuildCurrentModuleProgressAsync(studentProfileId, ct);

        return new ProgressStatsDto(
            ActivitiesCompleted: activitiesCompleted,
            TotalAttempts: totalAttempts,
            RetryAttempts: retryAttempts,
            AverageScore: averageScore,
            LatestScore: latestScore,
            BestScore: bestScore,
            ActivitiesThisWeek: activitiesThisWeek,
            ModulesCompleted: modulesCompleted,
            CurrentModuleProgress: currentModule);
    }

    private async Task<int> CountModulesCompletedAsync(Guid studentProfileId, CancellationToken ct)
    {
        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

        if (path is null) return 0;

        var modules = path.Modules.OrderBy(m => m.Order).ToList();
        var moduleIds = modules.Select(m => m.Id).ToList();
        var progressByModule = await _progressService.GetModuleProgressAsync(studentProfileId, moduleIds, ct);

        return modules.Count(m =>
        {
            if (m.IsCompleted) return true;
            var pd = progressByModule.GetValueOrDefault(m.Id);
            return pd is not null && pd.DistinctCompleted >= CompletionThreshold;
        });
    }

    private async Task<ProgressCurrentModuleDto?> BuildCurrentModuleProgressAsync(Guid studentProfileId, CancellationToken ct)
    {
        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

        if (path is null) return null;

        var modules = path.Modules.OrderBy(m => m.Order).ToList();
        var moduleIds = modules.Select(m => m.Id).ToList();
        var progressByModule = await _progressService.GetModuleProgressAsync(studentProfileId, moduleIds, ct);

        var currentModule = modules.FirstOrDefault(m =>
        {
            if (m.IsCompleted) return false;
            var pd = progressByModule.GetValueOrDefault(m.Id);
            return pd is null || pd.DistinctCompleted < CompletionThreshold;
        }) ?? modules.LastOrDefault();

        if (currentModule is null) return null;

        var progress = progressByModule.GetValueOrDefault(currentModule.Id);
        return new ProgressCurrentModuleDto(
            ModuleId: currentModule.Id,
            Title: currentModule.Title,
            CompletedActivities: progress?.DistinctCompleted ?? 0,
            TotalRequired: CompletionThreshold,
            AverageScore: progress?.AverageScore,
            LatestScore: progress?.LatestScore,
            IsReadyToComplete: progress?.IsReadyToComplete ?? false);
    }

    private async Task<IReadOnlyList<ScoreTrendPointDto>> BuildScoreTrendAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        // Join attempts → activities → modules for titles
        var trendData = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId && a.Score.HasValue)
            .OrderByDescending(a => a.CreatedAt)
            .Take(ScoreTrendLimit)
            .Join(
                _db.LearningActivities,
                attempt => attempt.LearningActivityId,
                activity => activity.Id,
                (attempt, activity) => new
                {
                    attempt.CreatedAt,
                    attempt.Score,
                    ActivityTitle = activity.Title,
                    activity.LearningModuleId,
                    attempt.LearningActivityId,
                })
            .ToListAsync(ct);

        if (trendData.Count == 0) return [];

        // Load module titles for those that have a module
        var moduleIds = trendData
            .Where(t => t.LearningModuleId.HasValue)
            .Select(t => t.LearningModuleId!.Value)
            .Distinct()
            .ToList();

        var moduleTitles = moduleIds.Count > 0
            ? await _db.LearningModules
                .Where(m => moduleIds.Contains(m.Id))
                .Select(m => new { m.Id, m.Title })
                .ToDictionaryAsync(m => m.Id, m => m.Title, ct)
            : new Dictionary<Guid, string>();

        // Calculate attempt numbers per activity
        var activityIds = trendData.Select(t => t.LearningActivityId).Distinct().ToList();
        var attemptOrders = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId && activityIds.Contains(a.LearningActivityId))
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.LearningActivityId, a.CreatedAt })
            .ToListAsync(ct);

        // Build attempt number index: attempt ID → attempt number (1-based) per activity
        var attemptNumberById = new Dictionary<Guid, int>();
        var grouped = attemptOrders.GroupBy(a => a.LearningActivityId);
        foreach (var group in grouped)
        {
            int num = 1;
            foreach (var a in group.OrderBy(x => x.CreatedAt))
                attemptNumberById[a.Id] = num++;
        }

        // Re-query to get attempt IDs for the trend data
        var trendWithIds = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId && a.Score.HasValue)
            .OrderByDescending(a => a.CreatedAt)
            .Take(ScoreTrendLimit)
            .Select(a => new { a.Id, a.LearningActivityId, a.Score, a.CreatedAt })
            .ToListAsync(ct);

        return trendData.Zip(trendWithIds, (t, withId) => new ScoreTrendPointDto(
            AttemptDate: t.CreatedAt,
            Score: Math.Round(t.Score!.Value, 0),
            ActivityTitle: t.ActivityTitle,
            ModuleTitle: t.LearningModuleId.HasValue
                ? moduleTitles.GetValueOrDefault(t.LearningModuleId.Value)
                : null,
            AttemptNumber: attemptNumberById.GetValueOrDefault(withId.Id, 1)))
            .ToList();
    }

    private async Task<ProgressSkillSectionDto> BuildSkillProgressAsync(Guid studentProfileId, CancellationToken ct)
    {
        var skillProfiles = await _db.StudentSkillProfiles
            .Where(s => s.StudentProfileId == studentProfileId)
            .OrderBy(s => s.SkillLabel)
            .ToListAsync(ct);

        var skills = skillProfiles
            .Select(s => new ProgressSkillDto(s.SkillKey, s.SkillLabel, s.IsWeak, s.ScorePercent))
            .ToList();

        var strengths = skills.Where(s => !s.IsWeak).Select(s => s.SkillLabel).Take(TopSkillCount).ToList();
        var weak = skills.Where(s => s.IsWeak).Select(s => s.SkillLabel).Take(TopSkillCount).ToList();

        return new ProgressSkillSectionDto(skills, strengths, weak);
    }

    private async Task<ProgressLearningFocusDto?> BuildLearningFocusAsync(Guid studentProfileId, CancellationToken ct)
    {
        var memory = await _db.UserLearningSummaries
            .FirstOrDefaultAsync(m => m.StudentProfileId == studentProfileId, ct);

        if (memory is null) return null;

        var strongSkills = DeserializeStringList(memory.StrongSkillsJson);
        var weakSkills = DeserializeStringList(memory.WeakSkillsJson);
        var nextFocus = DeserializeStringList(memory.NextFocusJson);
        var recurringMistakes = DeserializeStringList(memory.RecurringMistakesJson);

        // Only return if there's something meaningful to show
        if (string.IsNullOrEmpty(memory.JourneySummary)
            && nextFocus.Count == 0
            && recurringMistakes.Count == 0)
            return null;

        return new ProgressLearningFocusDto(
            JourneySummary: memory.JourneySummary,
            NextRecommendedFocus: nextFocus,
            RecurringMistakes: recurringMistakes,
            WeakSkills: weakSkills,
            StrongSkills: strongSkills);
    }

    private async Task<IReadOnlyList<ProgressModuleDto>> BuildModuleProgressAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

        if (path is null) return [];

        var modules = path.Modules.OrderBy(m => m.Order).ToList();
        var moduleIds = modules.Select(m => m.Id).ToList();
        var progressByModule = await _progressService.GetModuleProgressAsync(studentProfileId, moduleIds, ct);

        var currentModuleId = modules.FirstOrDefault(m =>
        {
            if (m.IsCompleted) return false;
            var pd = progressByModule.GetValueOrDefault(m.Id);
            return pd is null || pd.DistinctCompleted < CompletionThreshold;
        })?.Id;

        return modules.Select(m =>
        {
            var pd = progressByModule.GetValueOrDefault(m.Id);
            bool isCompleted = m.IsCompleted || (pd is not null && pd.DistinctCompleted >= CompletionThreshold);
            bool isCurrent = !isCompleted && m.Id == currentModuleId;

            string status = isCompleted ? "completed" : isCurrent ? "current" : "upcoming";

            return new ProgressModuleDto(
                ModuleId: m.Id,
                Title: m.Title,
                Status: status,
                CompletedActivities: pd?.DistinctCompleted ?? 0,
                TotalRequired: CompletionThreshold,
                AverageScore: pd?.AverageScore,
                LatestScore: pd?.LatestScore,
                IsReadyToComplete: pd?.IsReadyToComplete ?? false,
                CompletedAt: m.CompletedAt);
        }).ToList();
    }

    private static DateTime GetWeekStart(DateTime utcNow)
    {
        // ISO week: Monday = start of week
        int daysFromMonday = ((int)utcNow.DayOfWeek + 6) % 7;
        return utcNow.Date.AddDays(-daysFromMonday);
    }

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
