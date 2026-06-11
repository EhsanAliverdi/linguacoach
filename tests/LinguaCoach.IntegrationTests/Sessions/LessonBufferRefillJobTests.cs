using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Jobs;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using Quartz.Impl;

namespace LinguaCoach.IntegrationTests.Sessions;

/// <summary>
/// LessonBufferRefillJob: students at/below the refill threshold get a batch generation queued;
/// students above the threshold are skipped. The job uses a single GROUP BY query.
/// </summary>
public sealed class LessonBufferRefillJobTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public LessonBufferRefillJobTests(ActivityTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Execute_QueuesGenerationForStudentsBelowThreshold_SkipsAbove()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Settings: threshold = 1, batch = 4.
        var settings = await db.LessonGenerationSettings.FirstAsync();

        // Student LOW: 1 ready upcoming session (<= threshold) → should be queued.
        var low = await SeedStudentWithReadySessionsAsync(db, readyCount: 1);
        // Student HIGH: 5 ready upcoming sessions (> threshold) → should be skipped.
        var high = await SeedStudentWithReadySessionsAsync(db, readyCount: 5);

        var scheduler = await CreateInMemorySchedulerAsync();
        var factory = new SingleSchedulerFactory(scheduler);

        var job = new LessonBufferRefillJob(db, factory, NullLogger<LessonBufferRefillJob>.Instance);
        await job.Execute(new FakeJobExecutionContext(scheduler));

        // The low student should have a LessonBatchGenerationJob scheduled; the high student should not.
        var triggerGroups = await scheduler.GetTriggerKeys(
            Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.AnyGroup());
        var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup());

        var lowQueued = jobKeys.Any(k => k.Name.Contains(low.ToString("N")));
        var highQueued = jobKeys.Any(k => k.Name.Contains(high.ToString("N")));

        Assert.True(lowQueued, "Below-threshold student should be queued.");
        Assert.False(highQueued, "Above-threshold student should be skipped.");

        await scheduler.Shutdown();
    }

    private static async Task<Guid> SeedStudentWithReadySessionsAsync(LinguaCoachDbContext db, int readyCount)
    {
        var user = new LinguaCoach.Persistence.Identity.ApplicationUser
        {
            UserName = $"refill_{Guid.NewGuid():N}@test.com",
            Email = $"refill_{Guid.NewGuid():N}@test.com",
            Role = UserRole.Student,
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var profile = new StudentProfile(user.Id);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        var path = new LearningPath(profile.Id, "Path", "ctx");
        db.LearningPaths.Add(path);
        await db.SaveChangesAsync();
        var module = new LearningModule(path.Id, "Module", "desc", 1);
        db.LearningModules.Add(module);
        await db.SaveChangesAsync();

        for (var i = 0; i < readyCount; i++)
        {
            var session = new LearningSession(module.Id, $"S{i}", "topic", "goal", 15, "writing", i);
            session.SetGenerationMetadata(profile.Id, i + 1, null);
            session.MarkGenerationReady(); // ready + NotStarted
            db.LearningSessions.Add(session);
        }
        await db.SaveChangesAsync();

        return profile.Id;
    }

    private static async Task<IScheduler> CreateInMemorySchedulerAsync()
    {
        var props = new System.Collections.Specialized.NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = $"test-{Guid.NewGuid():N}",
            ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
            ["quartz.threadPool.threadCount"] = "0"
        };
        var factory = new StdSchedulerFactory(props);
        return await factory.GetScheduler();
    }

    private sealed class SingleSchedulerFactory : ISchedulerFactory
    {
        private readonly IScheduler _scheduler;
        public SingleSchedulerFactory(IScheduler scheduler) => _scheduler = scheduler;
        public Task<IScheduler> GetScheduler(CancellationToken ct = default) => Task.FromResult(_scheduler);
        public Task<IScheduler?> GetScheduler(string schedName, CancellationToken ct = default) => Task.FromResult<IScheduler?>(_scheduler);
        public Task<IReadOnlyList<IScheduler>> GetAllSchedulers(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IScheduler>>(new[] { _scheduler });
    }
}
