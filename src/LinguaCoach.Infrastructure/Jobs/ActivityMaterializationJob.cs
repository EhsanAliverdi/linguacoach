using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Generates LearningActivity content for every SessionExercise in a generation batch's
/// sessions and marks each session Ready once all its exercises have activities.
///
/// Does NOT generate listening audio — that is TtsAudioGenerationJob, which this job triggers.
/// This must never run on the lesson page-load path.
///
/// Idempotent: exercises that already have a LearningActivityId are skipped.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ActivityMaterializationJob : IJob
{
    public const string JobName = "activity-materialization";
    public const string BatchIdKey = "batchId";

    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly IExercisePatternRepository _patternRepo;
    private readonly StudentProgressService _progress;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILearningGoalContextResolver _goalContextResolver;
    private readonly ILogger<ActivityMaterializationJob> _logger;

    public ActivityMaterializationJob(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        IExercisePatternRepository patternRepo,
        StudentProgressService progress,
        ISchedulerFactory schedulerFactory,
        ILearningGoalContextResolver goalContextResolver,
        ILogger<ActivityMaterializationJob> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _patternRepo = patternRepo;
        _progress = progress;
        _schedulerFactory = schedulerFactory;
        _goalContextResolver = goalContextResolver;
        _logger = logger;
    }

    public static async Task TriggerAsync(IScheduler scheduler, Guid batchId, CancellationToken ct)
    {
        var job = JobBuilder.Create<ActivityMaterializationJob>()
            .WithIdentity($"{JobName}-{batchId:N}-{Guid.NewGuid():N}")
            .UsingJobData(BatchIdKey, batchId.ToString())
            .Build();
        await scheduler.ScheduleJob(job, TriggerBuilder.Create().StartNow().Build(), ct);
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var batchId = Guid.Parse(context.MergedJobDataMap.GetString(BatchIdKey)!);

        var sessions = await _db.LearningSessions
            .Where(s => s.GenerationBatchId == batchId && s.GenerationStatus != GenerationStatus.Ready)
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            _logger.LogInformation("ActivityMaterializationJob: no pending sessions for BatchId={BatchId}.", batchId);
            return;
        }

        foreach (var session in sessions)
        {
            if (session.StudentProfileId is null) continue;
            var profile = await _db.StudentProfiles
                .Include(p => p.CareerProfile)
                .Include(p => p.LanguagePair!).ThenInclude(lp => lp.SourceLanguage)
                .Include(p => p.LanguagePair!).ThenInclude(lp => lp.TargetLanguage)
                .FirstOrDefaultAsync(p => p.Id == session.StudentProfileId.Value, ct);
            if (profile is null) continue;

            var exercises = await _db.SessionExercises
                .Where(e => e.LearningSessionId == session.Id)
                .OrderBy(e => e.Order)
                .ToListAsync(ct);

            var allReady = true;
            foreach (var exercise in exercises)
            {
                if (exercise.LearningActivityId.HasValue) continue;
                try
                {
                    await MaterializeExerciseAsync(profile, session, exercise, ct);
                }
                catch (Exception ex)
                {
                    allReady = false;
                    _logger.LogWarning(ex,
                        "ActivityMaterializationJob: exercise {ExerciseId} generation failed.", exercise.Id);
                }
            }

            if (allReady)
            {
                session.MarkGenerationReady();
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("ActivityMaterializationJob: session {SessionId} marked Ready.", session.Id);
            }
            else
            {
                session.MarkGenerationFailed();
                await _db.SaveChangesAsync(ct);
            }
        }

        // Queue audio generation for any listening activities now materialized.
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        await TtsAudioGenerationJob.TriggerAsync(scheduler, batchId, ct);
    }

    private async Task MaterializeExerciseAsync(
        StudentProfile profile, LearningSession session, SessionExercise exercise, CancellationToken ct)
    {
        // Review reflection steps — lightweight placeholder, no AI.
        if (exercise.ExercisePatternKey is "lesson_reflection")
        {
            var placeholder = new LearningActivity(
                ActivityType.WritingScenario, ActivitySource.SystemGenerated,
                $"Lesson reflection: {session.Title}", profile.CefrLevel ?? "B1",
                "{\"activityType\":\"Review\"}", session.LearningModuleId, exercisePatternKey: "lesson_reflection");
            _db.LearningActivities.Add(placeholder);
            await _db.SaveChangesAsync(ct);
            exercise.AssignActivity(placeholder.Id);
            await _db.SaveChangesAsync(ct);
            return;
        }

        var pattern = await _patternRepo.GetByKeyAsync(exercise.ExercisePatternKey, ct);
        ActivityType activityType;
        string? overridePromptKey = null;
        string? patternKey = null;
        if (pattern is not null && pattern.IsActive)
        {
            activityType = pattern.ActivityType;
            overridePromptKey = pattern.AiGeneratePromptKey;
            patternKey = pattern.Key;
        }
        else
        {
            activityType = ExercisePrepareHandler.MapKindToActivityType(
                ResolveKind(exercise.ExercisePatternKey));
        }

        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);
        var recentMistakes = StudentProgressService.BuildRecentMistakesSummary(focusArea);

        var pair = profile.LanguagePair;
        var context = new ActivityGenerationContext(
            ActivityType: activityType,
            CefrLevel: profile.CefrLevel ?? "B1",
            CareerContext: profile.CareerProfile?.Name ?? "General",
            LanguagePairCode: $"{pair?.SourceLanguage?.Code ?? "fa"}-{pair?.TargetLanguage?.Code ?? "en"}",
            SourceLanguageName: pair?.SourceLanguage?.Name ?? "Persian",
            TargetLanguageName: pair?.TargetLanguage?.Name ?? "English",
            TopicHint: $"{session.Title}: {exercise.Instructions}",
            RecentMistakesSummary: recentMistakes,
            OverridePromptKey: overridePromptKey,
            ExercisePatternKey: patternKey,
            LearnerPreferenceContext: LearnerPreferenceContextFormatter.Build(
                profile, pair?.TargetLanguage?.Name),
            LearningGoalContext: _goalContextResolver.Resolve(profile, new LearningGoalResolutionContext { Source = "ActivityMaterializationJob" }).ContextSummary);

        var contentJson = await _aiGenerator.GenerateActivityContentAsync(context, ct);

        var activity = new LearningActivity(
            activityType, ActivitySource.AiGenerated,
            ExtractTitle(contentJson) ?? $"{activityType} activity",
            profile.CefrLevel ?? "B1", contentJson,
            session.LearningModuleId, exercisePatternKey: patternKey);
        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync(ct);

        exercise.AssignActivity(activity.Id);
        await _db.SaveChangesAsync(ct);
    }

    private static ExerciseKind ResolveKind(string patternKey) => patternKey switch
    {
        "phrase_match" or "gap_fill_workplace_phrase" => ExerciseKind.VocabularyWarmup,
        "listen_and_answer" or "listen_and_gap_fill" => ExerciseKind.ListeningInput,
        "email_reply" or "teams_chat_simulation" or "writing_response" => ExerciseKind.WritingTask,
        "spoken_response_from_prompt" or "speaking_role_play" => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("listen", StringComparison.OrdinalIgnoreCase) => ExerciseKind.ListeningInput,
        _ when patternKey.StartsWith("speaking", StringComparison.OrdinalIgnoreCase) => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("writing", StringComparison.OrdinalIgnoreCase) => ExerciseKind.WritingTask,
        _ => ExerciseKind.ContextInput
    };

    private static string? ExtractTitle(string contentJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                return t.GetString();
        }
        catch { }
        return null;
    }
}
