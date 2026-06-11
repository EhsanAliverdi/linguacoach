using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Finds students whose ready-lesson buffer is below the refill threshold and
/// enqueues a LessonBatchGenerationJob for each.
///
/// Uses a single GROUP BY query over LearningSessions — never a per-student loop of queries.
/// Runs on a periodic Quartz schedule (every 15 minutes) and is also triggered inline
/// from session completion and placement completion.
/// </summary>
[DisallowConcurrentExecution]
public sealed class LessonBufferRefillJob : IJob
{
    public const string JobName = "lesson-buffer-refill";

    private readonly LinguaCoachDbContext _db;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<LessonBufferRefillJob> _logger;

    public LessonBufferRefillJob(
        LinguaCoachDbContext db,
        ISchedulerFactory schedulerFactory,
        ILogger<LessonBufferRefillJob> logger)
    {
        _db = db;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var settings = await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null || !settings.EnableBackgroundGeneration)
        {
            _logger.LogInformation("LessonBufferRefillJob: background generation disabled or unconfigured — skipping.");
            return;
        }

        // Single GROUP BY query: ready upcoming sessions per student.
        var readyCounts = await _db.LearningSessions
            .Where(s => s.StudentProfileId != null
                     && s.Status == SessionStatus.NotStarted
                     && s.GenerationStatus == GenerationStatus.Ready)
            .GroupBy(s => s.StudentProfileId!.Value)
            .Select(g => new { StudentProfileId = g.Key, ReadyCount = g.Count() })
            .ToListAsync(ct);

        var belowThreshold = readyCounts
            .Where(r => r.ReadyCount <= settings.RefillThreshold)
            .Select(r => r.StudentProfileId)
            .ToList();

        if (belowThreshold.Count == 0)
        {
            _logger.LogInformation("LessonBufferRefillJob: no students below refill threshold ({Threshold}).", settings.RefillThreshold);
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler(ct);
        foreach (var studentProfileId in belowThreshold)
        {
            await LessonBatchGenerationJob.TriggerAsync(
                scheduler, studentProfileId, GenerationTriggerReason.ScheduledRefill, settings.RefillBatchSize, ct);
            _logger.LogInformation(
                "LessonBufferRefillJob: queued generation StudentProfileId={StudentProfileId} BatchSize={BatchSize}",
                studentProfileId, settings.RefillBatchSize);
        }
    }
}
