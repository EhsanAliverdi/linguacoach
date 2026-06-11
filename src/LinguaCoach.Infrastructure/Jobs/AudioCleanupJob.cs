using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Deletes orphaned/failed audio objects and updates DB state. Runs daily.
///
/// Targets:
///   - AudioAssets with GenerationStatus = Failed older than 7 days
///   - AudioAssets whose linked activity/attempt no longer exists
/// </summary>
[DisallowConcurrentExecution]
public sealed class AudioCleanupJob : IJob
{
    public const string JobName = "audio-cleanup";

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ILogger<AudioCleanupJob> _logger;

    public AudioCleanupJob(LinguaCoachDbContext db, IFileStorageService storage, ILogger<AudioCleanupJob> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var failedOld = await _db.AudioAssets
            .Where(a => a.GenerationStatus == GenerationStatus.Failed && a.CreatedAtUtc < cutoff)
            .ToListAsync(ct);

        // Orphaned: linked activity no longer exists.
        var orphaned = await _db.AudioAssets
            .Where(a => a.LearningActivityId != null
                     && !_db.LearningActivities.Any(la => la.Id == a.LearningActivityId))
            .ToListAsync(ct);

        // Orphaned: linked attempt no longer exists.
        var orphanedAttempts = await _db.AudioAssets
            .Where(a => a.ActivityAttemptId != null
                     && !_db.ActivityAttempts.Any(at => at.Id == a.ActivityAttemptId))
            .ToListAsync(ct);

        var toDelete = failedOld
            .Concat(orphaned)
            .Concat(orphanedAttempts)
            .GroupBy(a => a.Id)
            .Select(g => g.First())
            .ToList();

        var deleted = 0;
        foreach (var asset in toDelete)
        {
            try
            {
                await _storage.DeleteAsync(asset.ObjectKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AudioCleanupJob: failed to delete object {Key}.", asset.ObjectKey);
            }
            _db.AudioAssets.Remove(asset);
            deleted++;
        }

        if (deleted > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("AudioCleanupJob: removed {Count} orphaned/failed audio assets.", deleted);
        }
    }
}
