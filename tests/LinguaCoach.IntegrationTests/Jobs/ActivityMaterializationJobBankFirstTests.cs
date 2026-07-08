using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Curriculum;
using LinguaCoach.Infrastructure.Jobs;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.IntegrationTests.Sessions;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;

namespace LinguaCoach.IntegrationTests.Jobs;

/// <summary>
/// Phase D1/D2 — bank-first Today slice. Proves ActivityMaterializationJob injects published
/// Resource Bank content into the AI prompt's TopicHint for vocabulary/reading patterns when
/// matching bank rows exist at the student's CEFR level, that it falls back to today's unchanged
/// legacy behavior (no bank marker) when no matching bank rows exist, and (Phase D2) that the
/// full selected-resource list is durably recorded on LearningActivity.BankResourceProvenanceJson.
/// Uses a context-capturing fake IAiActivityGenerator — no real/live AI provider anywhere in this
/// suite.
/// </summary>
public sealed class ActivityMaterializationJobBankFirstTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ActivityMaterializationJobBankFirstTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Injects_bank_resource_context_when_matching_published_vocabulary_exists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (profileId, batchId, exerciseId) = await SeedPendingVocabularyExerciseAsync(db, cefrLevel: "B1");

        var source = new CefrResourceSource("D1 Test Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();
        db.CefrVocabularyEntries.Add(new CefrVocabularyEntry(source.Id, "deadline", "B1"));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Bank resources:");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        exercise.LearningActivityId.Should().NotBeNull();

        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().NotBeNullOrWhiteSpace();
        activity.BankResourceProvenanceJson.Should().Contain("Vocabulary");
    }

    [Fact]
    public async Task Falls_back_to_legacy_generation_unchanged_when_no_matching_bank_rows_exist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // No CefrVocabularyEntry rows seeded by this test at C1 — and the app-startup E6 seed pack
        // (InternalResourceSeedPackSeeder, wired into Program.cs and run by WebApplicationFactory
        // startup for this fixture) only covers A1-B2, so C1 is genuinely empty for this bank type.
        var (_, batchId, exerciseId) = await SeedPendingVocabularyExerciseAsync(db, cefrLevel: "C1");

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().NotContain("Bank resources:");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        exercise.LearningActivityId.Should().NotBeNull();

        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().BeNull();
    }

    [Fact]
    public async Task Injects_bank_resource_context_for_reading_multiple_choice_multi_confirming_broadened_pattern_coverage()
    {
        // Phase D2 finding: TodayBankResourceSelector gates purely on pattern.PrimarySkill, not an
        // explicit pattern-key allow-list — so every Reading-primary-skill pattern was already
        // covered, not just the 3 patterns D1's own docs originally called out. This test proves
        // reading_multiple_choice_multi (never explicitly mentioned in D1) gets bank context too.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "B1", patternKey: ExercisePatternKey.ReadingMultipleChoiceMulti, primarySkill: "Reading");

        var source = new CefrResourceSource("D2 Test Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();
        db.CefrReadingReferences.Add(new CefrReadingReference(source.Id, "B1", referenceExcerpt: "A short workplace excerpt."));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Bank resources:");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().Contain("Reading");
    }

    private static Task<(Guid ProfileId, Guid BatchId, Guid ExerciseId)> SeedPendingVocabularyExerciseAsync(
        LinguaCoachDbContext db, string cefrLevel) =>
        SeedPendingExerciseAsync(db, cefrLevel, ExercisePatternKey.PhraseMatch, "Vocabulary");

    private static async Task<(Guid ProfileId, Guid BatchId, Guid ExerciseId)> SeedPendingExerciseAsync(
        LinguaCoachDbContext db, string cefrLevel, string patternKey, string primarySkill)
    {
        var user = new ApplicationUser
        {
            UserName = $"d1_{Guid.NewGuid():N}@test.com",
            Email = $"d1_{Guid.NewGuid():N}@test.com",
            Role = UserRole.Student,
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var profile = new StudentProfile(user.Id);
        profile.SetCefrLevel(cefrLevel);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        var path = new LearningPath(profile.Id, "Path", "General workplace English context.");
        db.LearningPaths.Add(path);
        await db.SaveChangesAsync();

        var module = new LearningModule(path.Id, "Module", "desc", 0);
        db.LearningModules.Add(module);
        await db.SaveChangesAsync();

        var batch = new GenerationBatch(profile.Id, GenerationTriggerReason.ManualAdmin, requestedSessionCount: 1);
        db.GenerationBatches.Add(batch);
        await db.SaveChangesAsync();

        var session = new LearningSession(module.Id, "Session", "topic", "goal", 15, primarySkill, 0);
        session.SetGenerationMetadata(profile.Id, 1, batch.Id);
        session.MarkGenerationPending();
        db.LearningSessions.Add(session);
        await db.SaveChangesAsync();

        var exercise = new SessionExercise(
            session.Id, 0, patternKey, primarySkill, "Practise key workplace content.", 3);
        db.SessionExercises.Add(exercise);
        await db.SaveChangesAsync();

        return (profile.Id, batch.Id, exercise.Id);
    }

    private static async Task<(ActivityMaterializationJob Job, CapturingLogger<ActivityMaterializationJob> Logger)> BuildJobAsync(
        IServiceScope scope, LinguaCoachDbContext db, IAiActivityGenerator aiGenerator)
    {
        var scheduler = await CreateInMemorySchedulerAsync();
        var logger = new CapturingLogger<ActivityMaterializationJob>();
        var job = new ActivityMaterializationJob(
            db,
            aiGenerator,
            scope.ServiceProvider.GetRequiredService<IExercisePatternRepository>(),
            scope.ServiceProvider.GetRequiredService<LinguaCoach.Infrastructure.Progress.StudentProgressService>(),
            new SingleSchedulerFactory(scheduler),
            scope.ServiceProvider.GetRequiredService<ILearningGoalContextResolver>(),
            scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>(),
            scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>(),
            scope.ServiceProvider.GetRequiredService<IActivityNoveltyPolicy>(),
            scope.ServiceProvider.GetRequiredService<IActivityContentFingerprintService>(),
            scope.ServiceProvider.GetRequiredService<ITodayBankResourceSelector>(),
            logger);
        return (job, logger);
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

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
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

    /// <summary>Fake IAiActivityGenerator that succeeds and records the received context, so tests
    /// can assert on exactly what was passed into the prompt (e.g. the injected bank supplement in
    /// TopicHint) without depending on a real/live AI provider.</summary>
    private sealed class ContextCapturingAiActivityGenerator : IAiActivityGenerator
    {
        public ActivityGenerationContext? LastContext { get; private set; }

        public Task<string> GenerateActivityContentAsync(ActivityGenerationContext context, CancellationToken ct)
        {
            LastContext = context;
            return Task.FromResult("""{"title":"Practice"}""");
        }

        public Task<string> EvaluateAttemptAsync(ActivityEvaluationContext context, CancellationToken ct)
            => throw new NotSupportedException("Not used by this test.");
    }
}
