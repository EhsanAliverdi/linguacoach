using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Jobs;
using LinguaCoach.Infrastructure.Learning;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using Quartz.Impl;

namespace LinguaCoach.IntegrationTests.Sessions;

public sealed class LessonBatchGenerationJobTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public LessonBatchGenerationJobTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Execute_WithValidPlan_MaterializesReadySessions()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfileId = await SeedStudentWithPathAsync(db);

        var scheduler = await CreateInMemorySchedulerAsync();
        var ai = new AiExecutionService(
            db,
            new LessonPlanProviderResolver(),
            scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.UsageGovernance.IUsageQuotaService>(),
            NullLogger<AiExecutionService>.Instance);

        var routing = scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>();
        var mastery = scope.ServiceProvider.GetRequiredService<IStudentMasteryEvaluationService>();
        var readinessPool = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var jobLogger = new CapturingLogger<LessonBatchGenerationJob>();
        var job = new LessonBatchGenerationJob(
            db,
            ai,
            new SingleSchedulerFactory(scheduler),
            new LearningGoalContextResolver(),
            routing,
            mastery,
            readinessPool,
            jobLogger);

        await job.Execute(new FakeJobExecutionContext(scheduler, new JobDataMap
        {
            [LessonBatchGenerationJob.StudentProfileIdKey] = studentProfileId.ToString(),
            [LessonBatchGenerationJob.TriggerReasonKey] = ((int)GenerationTriggerReason.ManualAdmin).ToString(),
            [LessonBatchGenerationJob.RequestedCountKey] = "2"
        }));

        var batch = await db.GenerationBatches
            .AsNoTracking()
            .SingleAsync(b => b.StudentProfileId == studentProfileId);
        Assert.True(
            batch.Status == GenerationBatchStatus.Completed,
            DescribeFailure(jobLogger.LastException, batch));
        Assert.Equal(2, batch.CompletedSessionCount);

        var sessions = await db.LearningSessions
            .AsNoTracking()
            .Where(s => s.StudentProfileId == studentProfileId)
            .OrderBy(s => s.CourseSequenceNumber)
            .ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s => Assert.Equal(GenerationStatus.Ready, s.GenerationStatus));
        Assert.Equal(new int?[] { 1, 2 }, sessions.Select(s => s.CourseSequenceNumber).ToArray());

        var exerciseCount = await db.SessionExercises
            .AsNoTracking()
            .CountAsync(e => sessions.Select(s => s.Id).Contains(e.LearningSessionId));
        Assert.Equal(2, exerciseCount);

        await scheduler.Shutdown();
    }

    private static async Task<Guid> SeedStudentWithPathAsync(LinguaCoachDbContext db)
    {
        var user = new ApplicationUser
        {
            UserName = $"batch_{Guid.NewGuid():N}@test.com",
            Email = $"batch_{Guid.NewGuid():N}@test.com",
            Role = UserRole.Student,
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var profile = new StudentProfile(user.Id);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        var path = new LearningPath(profile.Id, "Existing Path", "Existing learner context.");
        db.LearningPaths.Add(path);
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

    private static string DescribeFailure(Exception? exception, GenerationBatch batch)
    {
        if (exception is DbUpdateConcurrencyException concurrencyException)
        {
            var entries = string.Join(", ", concurrencyException.Entries.Select(e => e.Entity.GetType().Name));
            return $"{exception}{Environment.NewLine}Concurrency entries: {entries}";
        }

        return exception?.ToString() ?? batch.FailureReason ?? $"Batch status was {batch.Status}.";
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
                LastException = exception;
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class LessonPlanProviderResolver : IAiProviderResolver
    {
        private readonly IAiProvider _provider = new LessonPlanProvider();

        public AiProviderPair ResolveLlm(string featureKey, string categoryKey)
            => new(new AiProviderSelection(_provider, _provider.ProviderName, "fake-lesson-plan"), Fallback: null);

        public AiTtsProviderSelection ResolveTts(string featureKey, string categoryKey)
            => new("fake", "fake", "fake");
    }

    private sealed class LessonPlanProvider : IAiProvider
    {
        public string ProviderName => "fake-lesson-plan-provider";

        public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
        {
            const string json = """
            [
              {
                "title": "Polite follow-up",
                "topic": "Follow-up messages",
                "sessionGoal": "Write a polite follow-up message",
                "focusSkill": "writing",
                "durationMinutes": 15,
                "exercises": [
                  {
                    "exercisePatternKey": "email_reply",
                    "primarySkill": "writing",
                    "instructions": "Reply politely to a delayed response.",
                    "estimatedMinutes": 8
                  }
                ]
              },
              {
                "title": "Clear team update",
                "topic": "Team updates",
                "sessionGoal": "Give a concise project update",
                "focusSkill": "speaking",
                "durationMinutes": 15,
                "exercises": [
                  {
                    "exercisePatternKey": "spoken_response_from_prompt",
                    "primarySkill": "speaking",
                    "instructions": "Record a short project update.",
                    "estimatedMinutes": 8
                  }
                ]
              }
            ]
            """;

            return Task.FromResult(new AiResponse(json, 100, 100, 0m, "fake-lesson-plan", ProviderName));
        }
    }
}
