using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Builds a compact learner summary, asks AI for the next batch of lesson session plans,
/// validates the returned JSON strictly, creates a GenerationBatch, and queues
/// SessionMaterializationJob items (materialized inline here for reliability).
///
/// Idempotency: sessions are keyed by StudentProfileId + CourseSequenceNumber. Re-running
/// the job will not create duplicate sessions for sequence numbers that already exist.
/// </summary>
[DisallowConcurrentExecution]
public sealed class LessonBatchGenerationJob : IJob
{
    public const string JobName = "lesson-batch-generation";
    public const string StudentProfileIdKey = "studentProfileId";
    public const string TriggerReasonKey = "triggerReason";
    public const string RequestedCountKey = "requestedCount";

    private readonly LinguaCoachDbContext _db;
    private readonly AiExecutionService _ai;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<LessonBatchGenerationJob> _logger;

    public LessonBatchGenerationJob(
        LinguaCoachDbContext db,
        AiExecutionService ai,
        ISchedulerFactory schedulerFactory,
        ILogger<LessonBatchGenerationJob> logger)
    {
        _db = db;
        _ai = ai;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    /// <summary>Schedules a one-off batch generation for a student.</summary>
    public static async Task TriggerAsync(
        IScheduler scheduler,
        Guid studentProfileId,
        GenerationTriggerReason reason,
        int requestedCount,
        CancellationToken ct)
    {
        var job = JobBuilder.Create<LessonBatchGenerationJob>()
            .WithIdentity($"{JobName}-{studentProfileId:N}-{Guid.NewGuid():N}")
            .UsingJobData(StudentProfileIdKey, studentProfileId.ToString())
            .UsingJobData(TriggerReasonKey, ((int)reason).ToString())
            .UsingJobData(RequestedCountKey, requestedCount.ToString())
            .Build();

        var trigger = TriggerBuilder.Create().StartNow().Build();
        await scheduler.ScheduleJob(job, trigger, ct);
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var data = context.MergedJobDataMap;
        var studentProfileId = Guid.Parse(data.GetString(StudentProfileIdKey)!);
        var reason = (GenerationTriggerReason)int.Parse(data.GetString(TriggerReasonKey)!);
        var requestedCount = data.ContainsKey(RequestedCountKey) ? int.Parse(data.GetString(RequestedCountKey)!) : 4;
        var correlationId = context.FireInstanceId;

        var settings = await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null || !settings.EnableBackgroundGeneration)
        {
            _logger.LogInformation("LessonBatchGenerationJob: background generation disabled — skipping {StudentProfileId}.", studentProfileId);
            return;
        }
        if (requestedCount < 1) requestedCount = settings.RefillBatchSize;

        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .Include(p => p.LanguagePair!).ThenInclude(lp => lp.SourceLanguage)
            .Include(p => p.LanguagePair!).ThenInclude(lp => lp.TargetLanguage)
            .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct);
        if (profile is null)
        {
            _logger.LogWarning("LessonBatchGenerationJob: student profile {StudentProfileId} not found.", studentProfileId);
            return;
        }

        var summaryJson = await BuildCompactSummaryAsync(profile, ct);

        var batch = new GenerationBatch(studentProfileId, reason, requestedCount, correlationId);
        _db.GenerationBatches.Add(batch);
        var planItem = batch.AddItem(GenerationJobItemType.SessionPlan);
        await _db.SaveChangesAsync(ct);

        batch.MarkRunning(summarySnapshotJson: summaryJson);
        planItem.MarkRunning();
        await _db.SaveChangesAsync(ct);

        string responseJson;
        try
        {
            var prompt = await RenderPromptAsync(summaryJson, requestedCount, ct);
            responseJson = await _ai.ExecuteAsync(
                LinguaCoach.Persistence.Seed.DefaultAiSeeder.LessonBatchPlanKey,
                new AiRequest(LinguaCoach.Persistence.Seed.DefaultAiSeeder.LessonBatchPlanKey, prompt, 2500),
                studentProfileId, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LessonBatchGenerationJob: AI planning failed StudentProfileId={StudentProfileId}", studentProfileId);
            planItem.MarkFailed("AI planning failed.");
            batch.MarkFailed("Could not generate the lesson plan.");
            await _db.SaveChangesAsync(ct);
            return;
        }

        List<SessionPlanDto> plans;
        try
        {
            plans = ParseAndValidatePlans(responseJson, requestedCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LessonBatchGenerationJob: invalid plan JSON StudentProfileId={StudentProfileId}", studentProfileId);
            planItem.MarkFailed("AI returned an invalid lesson plan.");
            batch.MarkFailed("The generated lesson plan was invalid.");
            await _db.SaveChangesAsync(ct);
            return;
        }

        planItem.MarkCompleted();
        await _db.SaveChangesAsync(ct);

        // Materialize sessions inline (idempotent by StudentProfileId + CourseSequenceNumber).
        var module = await EnsureGeneratedModuleAsync(profile, ct);
        var nextSequence = await NextCourseSequenceAsync(studentProfileId, ct);
        var moduleOrderBase = await _db.LearningSessions.CountAsync(s => s.LearningModuleId == module.Id, ct);

        foreach (var plan in plans)
        {
            var sequence = nextSequence++;

            var exists = await _db.LearningSessions.AnyAsync(
                s => s.StudentProfileId == studentProfileId && s.CourseSequenceNumber == sequence, ct);
            if (exists)
            {
                _logger.LogInformation("LessonBatchGenerationJob: session sequence {Sequence} already exists — skipping.", sequence);
                continue;
            }

            var sessionItem = batch.AddItem(GenerationJobItemType.Session);
            sessionItem.MarkRunning();

            var session = new LearningSession(
                module.Id,
                plan.Title,
                plan.Topic,
                plan.SessionGoal,
                plan.DurationMinutes,
                plan.FocusSkill,
                order: moduleOrderBase++,
                generatedFromMemorySnapshotJson: summaryJson);
            session.SetGenerationMetadata(studentProfileId, sequence, batch.Id);
            session.MarkGenerationPending();
            _db.LearningSessions.Add(session);
            await _db.SaveChangesAsync(ct);

            var order = 0;
            foreach (var ex in plan.Exercises)
            {
                var exercise = new SessionExercise(
                    session.Id,
                    order++,
                    ex.ExercisePatternKey,
                    ex.PrimarySkill,
                    ex.Instructions,
                    ex.EstimatedMinutes);
                _db.SessionExercises.Add(exercise);
            }
            await _db.SaveChangesAsync(ct);

            sessionItem.SetTarget(session.Id);
            sessionItem.MarkCompleted();
            batch.IncrementCompleted();

            // Queue activity content materialization (not on page-load path).
            batch.AddItem(GenerationJobItemType.Activity, session.Id);
            await _db.SaveChangesAsync(ct);
        }

        batch.MarkCompleted();
        await _db.SaveChangesAsync(ct);

        // Trigger follow-up materialization for activity content + audio.
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        await ActivityMaterializationJob.TriggerAsync(scheduler, batch.Id, ct);

        _logger.LogInformation(
            "LessonBatchGenerationJob: completed BatchId={BatchId} StudentProfileId={StudentProfileId} Sessions={Count}",
            batch.Id, studentProfileId, batch.CompletedSessionCount);
    }

    private async Task<int> NextCourseSequenceAsync(Guid studentProfileId, CancellationToken ct)
    {
        var max = await _db.LearningSessions
            .Where(s => s.StudentProfileId == studentProfileId && s.CourseSequenceNumber != null)
            .MaxAsync(s => (int?)s.CourseSequenceNumber, ct);
        return (max ?? 0) + 1;
    }

    private async Task<LearningModule> EnsureGeneratedModuleAsync(StudentProfile profile, CancellationToken ct)
    {
        var path = await _db.LearningPaths
            .Where(p => p.StudentProfileId == profile.Id && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (path is null)
        {
            path = new Domain.Entities.LearningPath(profile.Id, "Guided Lessons", "Background-generated lesson buffer.");
            _db.LearningPaths.Add(path);
            await _db.SaveChangesAsync(ct);
        }

        var module = await _db.LearningModules
            .Where(m => m.LearningPathId == path.Id && m.Title == "Generated Lessons")
            .FirstOrDefaultAsync(ct);

        if (module is null)
        {
            var order = await _db.LearningModules.CountAsync(m => m.LearningPathId == path.Id, ct);
            module = new LearningModule(path.Id, "Generated Lessons", "Lessons generated by the background buffer.", order);
            _db.LearningModules.Add(module);
            await _db.SaveChangesAsync(ct);
        }

        return module;
    }

    private async Task<string> RenderPromptAsync(string summaryJson, int sessionCount, CancellationToken ct)
    {
        var prompt = await _db.AiPrompts
            .Where(p => p.Key == LinguaCoach.Persistence.Seed.DefaultAiSeeder.LessonBatchPlanKey && p.IsActive)
            .Select(p => p.Content)
            .FirstOrDefaultAsync(ct)
            ?? "Plan {{sessionCount}} lessons from: {{summary}}. Return a JSON array.";

        return prompt
            .Replace("{{summary}}", summaryJson)
            .Replace("{{sessionCount}}", sessionCount.ToString());
    }

    private async Task<string> BuildCompactSummaryAsync(StudentProfile profile, CancellationToken ct)
    {
        var completedSessions = await _db.LearningSessions
            .CountAsync(s => s.StudentProfileId == profile.Id && s.Status == SessionStatus.Completed, ct);

        var skills = await _db.StudentSkillProfiles
            .Where(s => s.StudentProfileId == profile.Id)
            .ToListAsync(ct);

        var coveredScenarios = await _db.LearningSessions
            .Where(s => s.StudentProfileId == profile.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.Topic)
            .Take(8)
            .ToListAsync(ct);

        var summary = new
        {
            studentLevel = profile.CefrLevel ?? "B1",
            domainComplexity = profile.WorkplaceSeniority?.ToString() ?? "intermediate_workplace",
            careerContext = profile.CareerProfile?.Name ?? "General",
            sourceLanguage = profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
            targetLanguage = profile.LanguagePair?.TargetLanguage?.Name ?? "English",
            completedSessions,
            weakSkills = skills.Where(s => s.IsWeak).Select(s => s.SkillLabel).ToList(),
            strongSkills = skills.Where(s => !s.IsWeak).Select(s => s.SkillLabel).ToList(),
            coveredScenarios,
            avoidRepeating = coveredScenarios
        };

        return JsonSerializer.Serialize(summary);
    }

    internal static List<SessionPlanDto> ParseAndValidatePlans(string responseJson, int expectedCount)
    {
        var json = StripCodeFences(responseJson);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Lesson plan must be a JSON array.");

        var plans = new List<SessionPlanDto>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var title = GetString(el, "title");
            var topic = GetString(el, "topic", fallback: title);
            var goal = GetString(el, "sessionGoal", fallback: title);
            var focus = GetString(el, "focusSkill", fallback: "vocabulary");
            var duration = el.TryGetProperty("durationMinutes", out var d) && d.TryGetInt32(out var dm) && dm > 0 ? dm : 15;

            var exercises = new List<ExercisePlanDto>();
            if (el.TryGetProperty("exercises", out var exArr) && exArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var ex in exArr.EnumerateArray())
                {
                    var patternKey = GetString(ex, "exercisePatternKey", fallback: "phrase_match");
                    var primary = GetString(ex, "primarySkill", fallback: focus);
                    var instr = GetString(ex, "instructions", fallback: "Complete this exercise.");
                    var est = ex.TryGetProperty("estimatedMinutes", out var em) && em.TryGetInt32(out var emi) && emi > 0 ? emi : 5;
                    exercises.Add(new ExercisePlanDto(patternKey, primary, instr, est));
                }
            }

            if (string.IsNullOrWhiteSpace(title) || exercises.Count == 0)
                throw new InvalidOperationException("Each session plan needs a title and at least one exercise.");

            plans.Add(new SessionPlanDto(title, topic, goal, focus, duration, exercises));
        }

        if (plans.Count == 0)
            throw new InvalidOperationException("Lesson plan array was empty.");

        return plans;
    }

    private static string GetString(JsonElement el, string prop, string? fallback = null)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        }
        return fallback ?? string.Empty;
    }

    private static string StripCodeFences(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNl = trimmed.IndexOf('\n');
            if (firstNl >= 0) trimmed = trimmed[(firstNl + 1)..];
            if (trimmed.EndsWith("```")) trimmed = trimmed[..^3];
        }
        return trimmed.Trim();
    }

    internal sealed record SessionPlanDto(
        string Title, string Topic, string SessionGoal, string FocusSkill, int DurationMinutes, List<ExercisePlanDto> Exercises);

    internal sealed record ExercisePlanDto(
        string ExercisePatternKey, string PrimarySkill, string Instructions, int EstimatedMinutes);
}
