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

            // Periodic lesson buffer refill — every 15 minutes.
            var refillKey = new JobKey(LessonBufferRefillJob.JobName);
            q.AddJob<LessonBufferRefillJob>(opts => opts.WithIdentity(refillKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(refillKey)
                .WithIdentity($"{LessonBufferRefillJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(15).RepeatForever()));

            // Practice Gym buffer refill — every 30 minutes.
            var practiceKey = new JobKey(PracticeGymBufferRefillJob.JobName);
            q.AddJob<PracticeGymBufferRefillJob>(opts => opts.WithIdentity(practiceKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(practiceKey)
                .WithIdentity($"{PracticeGymBufferRefillJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(30).RepeatForever()));

            // Practice Gym generation - fills queued cache rows every 10 minutes.
            var practiceGenerationKey = new JobKey(PracticeGymGenerationJob.JobName);
            q.AddJob<PracticeGymGenerationJob>(opts => opts.WithIdentity(practiceGenerationKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(practiceGenerationKey)
                .WithIdentity($"{PracticeGymGenerationJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(10).RepeatForever()));

            // Audio cleanup — daily.
            var cleanupKey = new JobKey(AudioCleanupJob.JobName);
            q.AddJob<AudioCleanupJob>(opts => opts.WithIdentity(cleanupKey).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(cleanupKey)
                .WithIdentity($"{AudioCleanupJob.JobName}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInHours(24).RepeatForever()));

            // Durable jobs scheduled ad hoc by triggers.
            q.AddJob<LessonBatchGenerationJob>(opts =>
                opts.WithIdentity(LessonBatchGenerationJob.JobName).StoreDurably());
            q.AddJob<ActivityMaterializationJob>(opts =>
                opts.WithIdentity(ActivityMaterializationJob.JobName).StoreDurably());
            q.AddJob<TtsAudioGenerationJob>(opts =>
                opts.WithIdentity(TtsAudioGenerationJob.JobName).StoreDurably());
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
