using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Deletes generation_validation_failures rows older than the configured retention window.
/// Runs daily. Does not delete AI usage logs or prompt history.
/// </summary>
[DisallowConcurrentExecution]
public sealed class GenerationValidationFailurePruneJob : IJob
{
    public const string JobName = "generation-validation-failure-prune";

    private const int DefaultRetentionDays = 90;
    private const int MinRetentionDays = 7;
    private const int MaxRetentionDays = 365;

    private readonly LinguaCoachDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<GenerationValidationFailurePruneJob> _logger;

    public GenerationValidationFailurePruneJob(
        LinguaCoachDbContext db,
        IConfiguration config,
        ILogger<GenerationValidationFailurePruneJob> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var configuredDays = _config.GetValue<int?>("GenerationQuality:RetentionDays") ?? DefaultRetentionDays;
        var retentionDays = Math.Clamp(configuredDays, MinRetentionDays, MaxRetentionDays);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        try
        {
            var old = await _db.GenerationValidationFailures
                .Where(f => f.CreatedAt < cutoff)
                .ToListAsync(ct);

            if (old.Count > 0)
            {
                _db.GenerationValidationFailures.RemoveRange(old);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "GenerationValidationFailurePruneJob: pruned {Count} rows older than {RetentionDays}d (cutoff {Cutoff:u}).",
                    old.Count, retentionDays, cutoff);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GenerationValidationFailurePruneJob: failed to prune generation validation failures (RetentionDays={RetentionDays}).",
                retentionDays);
        }
    }
}
