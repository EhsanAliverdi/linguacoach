using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Curriculum;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Materializes queued Practice Gym cache entries into ready LearningActivity rows.
/// The student path can still generate on demand if this cache is empty.
/// </summary>
[DisallowConcurrentExecution]
public sealed class PracticeGymGenerationJob : IJob
{
    public const string JobName = "practice-gym-generation";
    private const int MaxItemsPerRun = 20;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly IExercisePatternRepository _patternRepo;
    private readonly StudentProgressService _progress;
    private readonly ListeningAudioService _listeningAudio;
    private readonly ILearningGoalContextResolver _goalContextResolver;
    private readonly ICurriculumRoutingService _routing;
    private readonly IStudentMasteryEvaluationService _mastery;
    private readonly IStudentActivityReadinessPoolService _readinessPool;
    private readonly ILearningPlanService _learningPlan;
    private readonly ILogger<PracticeGymGenerationJob> _logger;

    public PracticeGymGenerationJob(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        IExercisePatternRepository patternRepo,
        StudentProgressService progress,
        ListeningAudioService listeningAudio,
        ILearningGoalContextResolver goalContextResolver,
        ICurriculumRoutingService routing,
        IStudentMasteryEvaluationService mastery,
        IStudentActivityReadinessPoolService readinessPool,
        ILearningPlanService learningPlan,
        ILogger<PracticeGymGenerationJob> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _patternRepo = patternRepo;
        _progress = progress;
        _listeningAudio = listeningAudio;
        _goalContextResolver = goalContextResolver;
        _routing = routing;
        _mastery = mastery;
        _readinessPool = readinessPool;
        _learningPlan = learningPlan;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var pending = await _db.PracticeActivityCache
            .Where(c => c.Status == PracticeCacheStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .Take(MaxItemsPerRun)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            _logger.LogInformation("PracticeGymGenerationJob: no pending cache rows.");
            return;
        }

        foreach (var cache in pending)
        {
            try
            {
                await MaterializeAsync(cache, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PracticeGymGenerationJob: failed to generate cache row {CacheId}.",
                    cache.Id);
                cache.MarkExpired();
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task MaterializeAsync(PracticeActivityCache cache, CancellationToken ct)
    {
        var exerciseType = await _db.ExerciseTypeDefinitions
            .FirstOrDefaultAsync(e => e.Key == cache.PatternKey
                                   && e.IsEnabled
                                   && e.ImplementationStatus == "ready"
                                   && e.SupportsPracticeGym, ct);
        if (exerciseType is null)
        {
            cache.MarkExpired();
            await _db.SaveChangesAsync(ct);
            return;
        }

        var pattern = await _patternRepo.GetByKeyAsync(cache.PatternKey, ct);
        if (pattern is null || !pattern.IsActive)
        {
            cache.MarkExpired();
            await _db.SaveChangesAsync(ct);
            return;
        }

        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .Include(p => p.LanguagePair!).ThenInclude(lp => lp.SourceLanguage)
            .Include(p => p.LanguagePair!).ThenInclude(lp => lp.TargetLanguage)
            .FirstOrDefaultAsync(p => p.Id == cache.StudentProfileId, ct);
        if (profile is null || profile.LifecycleStage == StudentLifecycleStage.Archived)
        {
            cache.MarkExpired();
            await _db.SaveChangesAsync(ct);
            return;
        }

        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);
        var recentMistakes = StudentProgressService.BuildRecentMistakesSummary(focusArea);
        var pair = profile.LanguagePair;

        var resolvedGoalContext = _goalContextResolver.Resolve(
            profile, new LearningGoalResolutionContext { Source = "PracticeGymGenerationJob" });

        var masteryReport = await _mastery.EvaluateStudentAsync(
            profile.Id, MasteryEvaluationReason.BeforeReplenishment, ct);

        // Phase 12D — consult learning plan for practice gym objective alignment.
        string? plannedObjectiveKey = null;
        try
        {
            var gymObjectives = await _learningPlan.GetPracticeGymObjectivesAsync(profile.Id, maxCount: 1, ct: ct);
            plannedObjectiveKey = gymObjectives.Count > 0 ? gymObjectives[0].ObjectiveKey : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PracticeGymGenerationJob: could not read learning plan for {StudentProfileId} — falling back to free routing.", profile.Id);
        }

        var routingRequest = CurriculumRoutingRequestFactory.Build(
            profile, resolvedGoalContext,
            source: "PracticeGymGenerationJob",
            requestedPatternKey: pattern.Key,
            allowReviewOrScaffold: false,
            masteredObjectiveKeys: masteryReport.MasteredObjectiveKeys,
            mode: RoutingMode.NewLearning,
            preferredObjectiveKey: plannedObjectiveKey);
        var routing = await _routing.RecommendAsync(routingRequest, ct);

        // Record pool item with routing snapshot before generation.
        var poolRequest = ReadinessItemRequestBuilder.FromRoutingRecommendation(
            studentId: profile.Id,
            source: ReadinessPoolSource.PracticeGym,
            recommendation: routing,
            originalCefrLevelSnapshot: profile.CefrLevel,
            difficultyPreference: profile.DifficultyPreference?.ToString(),
            supportLanguageCode: profile.LanguagePair?.SourceLanguage?.Code,
            supportLanguageName: profile.LanguagePair?.SourceLanguage?.Name,
            translationHelpPreference: profile.TranslationHelpPreference?.ToString(),
            patternKey: pattern.Key,
            activityType: pattern.ActivityType.ToString(),
            generatedBy: "PracticeGymGenerationJob");
        var poolItemId = await _readinessPool.CreateQueuedAsync(poolRequest, ct);
        await _readinessPool.MarkGeneratingAsync(poolItemId, ct);

        var generationContext = new ActivityGenerationContext(
            ActivityType: pattern.ActivityType,
            CefrLevel: routing.TargetCefrLevel,
            CareerContext: profile.CareerProfile?.Name ?? profile.CareerContext ?? "General",
            LanguagePairCode: $"{pair?.SourceLanguage?.Code ?? "fa"}-{pair?.TargetLanguage?.Code ?? "en"}",
            SourceLanguageName: pair?.SourceLanguage?.Name ?? "Persian",
            TargetLanguageName: pair?.TargetLanguage?.Name ?? "English",
            TopicHint: cache.SkillFocus ?? "English class practice",
            RecentMistakesSummary: recentMistakes,
            OverridePromptKey: pattern.AiGeneratePromptKey,
            ExercisePatternKey: pattern.Key,
            LearnerPreferenceContext: LearnerPreferenceContextFormatter.Build(
                profile, pair?.TargetLanguage?.Name),
            LearningGoalContext: resolvedGoalContext.ContextSummary,
            RoutingContext: routing.RoutingContextSummary,
            RoutingReason: routing.RoutingReason.ToString().ToLowerInvariant(),
            IsReviewOrScaffold: routing.IsLowerLevelContent);

        var contentJson = await _aiGenerator.GenerateActivityContentAsync(generationContext, ct);
        var title = ExtractTitle(contentJson) ?? pattern.Name;

        var activity = new LearningActivity(
            pattern.ActivityType,
            ActivitySource.AiGenerated,
            title,
            cache.CefrLevel,
            contentJson,
            learningModuleId: null,
            exercisePatternKey: pattern.Key);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync(ct);

        if (pattern.RequiresAudio || pattern.ActivityType == ActivityType.ListeningComprehension)
        {
            await _listeningAudio.EnsureAudioAsync(
                activity,
                pair?.TargetLanguage?.Code ?? "en",
                ct);
            await _db.SaveChangesAsync(ct);
        }

        cache.MarkReady(activity.Id);
        await _db.SaveChangesAsync(ct);

        await _readinessPool.MarkReadyAsync(poolItemId, learningActivityId: activity.Id, ct: ct);

        _logger.LogInformation(
            "PracticeGymGenerationJob: cache row {CacheId} ready with ActivityId={ActivityId}.",
            cache.Id,
            activity.Id);
    }

    private static string? ExtractTitle(string contentJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(contentJson);
            return doc.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == System.Text.Json.JsonValueKind.String
                    ? title.GetString()
                    : null;
        }
        catch
        {
            return null;
        }
    }
}
