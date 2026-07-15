using LinguaCoach.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using LinguaCoach.Persistence;
using Quartz;

namespace LinguaCoach.Api.Quartz;

/// <summary>
/// Wires Quartz.NET into the API process as an IHostedService.
///
/// Decisions (sprint D2/D6):
///   - Quartz runs inside LinguaCoach.Api (not LinguaCoach.Worker).
///   - Jobs use the Quartz.Extensions.DependencyInjection job factory so each job
///     execution gets a scoped DbContext.
///   - Quartz state is persisted to PostgreSQL (required) so queued work survives restarts.
///     If the connection string is missing, falls back to the in-memory store with a warning
///     (used in Development without a DB).
/// </summary>
public static class QuartzConfiguration
{
    public static IServiceCollection AddSpeakPathQuartz(this IServiceCollection services, IConfiguration config)
    {
        var enabled = config.GetValue<bool?>("BackgroundJobs:Enabled")
            ?? (Environment.GetEnvironmentVariable("BACKGROUND_JOBS_ENABLED") is "true" or null);
        if (!enabled)
            return services;

        var connectionString = config.GetConnectionString("DefaultConnection");
        var usePostgres = !string.IsNullOrWhiteSpace(connectionString);

        services.AddQuartz(q =>
        {
            if (usePostgres)
            {
                q.UsePersistentStore(store =>
                {
                    store.UseProperties = true;
                    store.UsePostgres(connectionString!);
                    store.UseNewtonsoftJsonSerializer();
                });
            }
            // else: default in-memory store (Development only).

            // Audio cleanup — daily.
            var cleanupKey = new JobKey(AudioCleanupJob.JobName);
            q.AddJob<AudioCleanupJob>(opts => opts.WithIdentity(cleanupKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(cleanupKey)
                .WithIdentity($"{AudioCleanupJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInHours(24).RepeatForever()));

            // Phase I2C: readiness pool replenishment job removed along with the readiness pool —
            // see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.

            // Notification dispatch — every 2 minutes.
            var dispatchKey = new JobKey(NotificationDispatchJob.JobName);
            q.AddJob<NotificationDispatchJob>(opts => opts.WithIdentity(dispatchKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(dispatchKey)
                .WithIdentity($"{NotificationDispatchJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(2).RepeatForever()));

            // Mastery re-evaluation sweep — daily (Phase 10Z).
            var masteryKey = new JobKey(StudentMasteryEvaluationJob.JobName);
            q.AddJob<StudentMasteryEvaluationJob>(opts => opts.WithIdentity(masteryKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(masteryKey)
                .WithIdentity($"{StudentMasteryEvaluationJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInHours(24).RepeatForever()));

            // Speaking evaluation — every 5 minutes. Resolves Pending evaluations.
            var speakingEvalKey = new JobKey(SpeakingEvaluationJob.JobName);
            q.AddJob<SpeakingEvaluationJob>(opts => opts.WithIdentity(speakingEvalKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(speakingEvalKey)
                .WithIdentity($"{SpeakingEvaluationJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

            // Speaking signal application — every 10 minutes. Applies config-gated mastery signals.
            var speakingSignalKey = new JobKey(SpeakingEvaluationSignalApplicationJob.JobName);
            q.AddJob<SpeakingEvaluationSignalApplicationJob>(opts => opts.WithIdentity(speakingSignalKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(speakingSignalKey)
                .WithIdentity($"{SpeakingEvaluationSignalApplicationJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(10).RepeatForever()));

            // Writing evaluation — every 5 minutes. Resolves Pending evaluations.
            var writingEvalKey = new JobKey(WritingEvaluationJob.JobName);
            q.AddJob<WritingEvaluationJob>(opts => opts.WithIdentity(writingEvalKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(writingEvalKey)
                .WithIdentity($"{WritingEvaluationJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

            // Writing signal application — every 10 minutes. Applies config-gated mastery signals.
            var writingSignalKey = new JobKey(WritingEvaluationSignalApplicationJob.JobName);
            q.AddJob<WritingEvaluationSignalApplicationJob>(opts => opts.WithIdentity(writingSignalKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(writingSignalKey)
                .WithIdentity($"{WritingEvaluationSignalApplicationJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(10).RepeatForever()));

            // Import package processing — every 2 minutes. Advances approved packages through
            // extraction/candidate creation; never touches an unapproved plan.
            var importPackageKey = new JobKey(ImportPackageProcessingJob.JobName);
            q.AddJob<ImportPackageProcessingJob>(opts => opts.WithIdentity(importPackageKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(importPackageKey)
                .WithIdentity($"{ImportPackageProcessingJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(2).RepeatForever()));

            // Generation validation failure retention prune — daily.
            var pruneKey = new JobKey(GenerationValidationFailurePruneJob.JobName);
            q.AddJob<GenerationValidationFailurePruneJob>(opts => opts.WithIdentity(pruneKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(pruneKey)
                .WithIdentity($"{GenerationValidationFailurePruneJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInHours(24).RepeatForever()));
        });

        services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);

        return services;
    }

    /// <summary>
    /// Validates that the Quartz PostgreSQL schema exists. Logs a warning if the
    /// qrtz_job_details table is missing — queued work would otherwise be silently dropped.
    /// </summary>
    public static async Task ValidateQuartzSchemaAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            if (!db.Database.IsNpgsql()) return;

            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT to_regclass('public.qrtz_job_details') IS NOT NULL";
            var result = await cmd.ExecuteScalarAsync(ct);
            var exists = result is bool b && b;
            if (!exists)
            {
                logger.LogWarning(
                    "Quartz schema not found (qrtz_job_details missing). Background jobs will not persist across restarts. " +
                    "Run the Quartz PostgreSQL schema script before relying on background generation.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not validate Quartz schema.");
        }
    }
}
