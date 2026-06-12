using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Maintains the Practice Gym ready-activity cache per student × enabled pattern key.
/// For each student/pattern below the per-type threshold, enqueues generation work
/// (a PracticeActivityCache row in Pending state that PracticeGymGenerationJob fills).
///
/// Uses a single GROUP BY query over PracticeActivityCache to count ready entries —
/// never a per-student loop of queries.
/// </summary>
[DisallowConcurrentExecution]
public sealed class PracticeGymBufferRefillJob : IJob
{
    public const string JobName = "practice-gym-buffer-refill";

    private static readonly string[] EnabledPatternKeys =
    {
        "phrase_match", "gap_fill_workplace_phrase", "listen_and_answer",
        "email_reply", "teams_chat_simulation", "spoken_response_from_prompt"
    };

    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<PracticeGymBufferRefillJob> _logger;

    public PracticeGymBufferRefillJob(LinguaCoachDbContext db, ILogger<PracticeGymBufferRefillJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var settings = await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null || !settings.EnableBackgroundGeneration)
        {
            _logger.LogInformation("PracticeGymBufferRefillJob: background generation disabled — skipping.");
            return;
        }

        // Single GROUP BY: ready cached activities per student × pattern.
        var readyCounts = await _db.PracticeActivityCache
            .Where(c => c.Status == PracticeCacheStatus.Ready)
            .GroupBy(c => new { c.StudentProfileId, c.PatternKey })
            .Select(g => new { g.Key.StudentProfileId, g.Key.PatternKey, Count = g.Count() })
            .ToListAsync(ct);

        var readyMap = readyCounts.ToDictionary(
            r => (r.StudentProfileId, r.PatternKey), r => r.Count);

        // Refill for students who can use the student app, including students with no cache yet.
        var students = await _db.StudentProfiles
            .Where(p => p.OnboardingStatus == OnboardingStatus.Complete
                     && p.LifecycleStage != StudentLifecycleStage.Archived
                     && p.LifecycleStage >= StudentLifecycleStage.CourseReady)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var pendingCounts = await _db.PracticeActivityCache
            .Where(c => c.Status == PracticeCacheStatus.Pending)
            .GroupBy(c => new { c.StudentProfileId, c.PatternKey })
            .Select(g => new { g.Key.StudentProfileId, g.Key.PatternKey, Count = g.Count() })
            .ToListAsync(ct);
        var pendingMap = pendingCounts.ToDictionary(
            r => (r.StudentProfileId, r.PatternKey), r => r.Count);

        var queued = 0;
        foreach (var studentProfileId in students)
        {
            foreach (var pattern in EnabledPatternKeys)
            {
                var ready = readyMap.GetValueOrDefault((studentProfileId, pattern), 0);
                var pending = pendingMap.GetValueOrDefault((studentProfileId, pattern), 0);
                if (ready + pending > settings.PracticeGymRefillThresholdPerType) continue;

                var toCreate = settings.PracticeGymRefillCountPerType;
                for (var i = 0; i < toCreate; i++)
                {
                    var fingerprint = GenerationHashing.Sha256($"{studentProfileId}:{pattern}:{Guid.NewGuid():N}")[..32];
                    _db.PracticeActivityCache.Add(new Domain.Entities.PracticeActivityCache(
                        studentProfileId, pattern, "B1", "intermediate_workplace", fingerprint));
                    queued++;
                }
            }
        }

        if (queued > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("PracticeGymBufferRefillJob: queued {Count} practice activities for generation.", queued);
        }
    }
}
