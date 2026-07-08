using LinguaCoach.Application.Ai;
using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Curriculum;
using LinguaCoach.Infrastructure.Jobs;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.IntegrationTests.Sessions;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace LinguaCoach.IntegrationTests.PracticeGym;

/// <summary>
/// Phase C1 (2026-07-08) generalized the Form.io Practice Gym pilot to a small first batch of
/// patterns; Phase C2 (2026-07-08) added a second small batch (reading_multiple_choice_multi,
/// reading_fill_in_blanks, reading_writing_fill_in_blanks); Phase C3 (2026-07-08) added
/// reorder_paragraphs (a stock Form.io "datagrid" with its built-in reorder setting). Uses fake AI
/// providers — no real API calls, per project convention. See docs/architecture/practice-gym.md.
/// </summary>
public sealed class PracticeGymTemplateGenerationJobTests : IClassFixture<PracticeGymTemplateTestFactory>
{
    private readonly PracticeGymTemplateTestFactory _factory;

    public PracticeGymTemplateGenerationJobTests(PracticeGymTemplateTestFactory factory) => _factory = factory;

    [Fact]
    public async Task MigratedPattern_WithApprovedTemplate_MaterializesViaTemplatePath()
    {
        _factory.UseReadingMcqProvider();
        await _factory.EnsureCreatedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        await EnablePilotFlagAsync(db);
        var profileId = await SeedStudentAsync(db);

        var cache = new PracticeActivityCache(
            profileId, "reading_multiple_choice_single", "B1", "general_workplace",
            contentFingerprint: Guid.NewGuid().ToString("N"));
        db.PracticeActivityCache.Add(cache);
        await db.SaveChangesAsync();

        var job = BuildJob(scope);
        var scheduler = await CreateInMemorySchedulerAsync();
        await job.Execute(new FakeJobExecutionContext(scheduler));

        var refreshedCache = await db.PracticeActivityCache.AsNoTracking().SingleAsync(c => c.Id == cache.Id);
        Assert.Equal(PracticeCacheStatus.Ready, refreshedCache.Status);
        Assert.NotNull(refreshedCache.LearningActivityId);

        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == refreshedCache.LearningActivityId);
        Assert.False(string.IsNullOrWhiteSpace(activity.FormIoSchemaJson));
        Assert.False(string.IsNullOrWhiteSpace(activity.ScoringRulesJson));
        Assert.DoesNotContain("correctAnswer", activity.FormIoSchemaJson, StringComparison.OrdinalIgnoreCase);
        await scheduler.Shutdown();

        var readinessItem = await db.StudentActivityReadinessItems.AsNoTracking()
            .SingleAsync(i => i.LearningActivityId == activity.Id);
        Assert.NotNull(readinessItem.SourceTemplateId);
    }

    [Fact]
    public async Task MigratedPattern_C2_ReadingMultipleChoiceMulti_MaterializesViaTemplatePath()
    {
        _factory.UseReadingMultiProvider();
        await _factory.EnsureCreatedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        await EnablePilotFlagAsync(db);
        var profileId = await SeedStudentAsync(db);

        var cache = new PracticeActivityCache(
            profileId, "reading_multiple_choice_multi", "B1", "general_workplace",
            contentFingerprint: Guid.NewGuid().ToString("N"));
        db.PracticeActivityCache.Add(cache);
        await db.SaveChangesAsync();

        var job = BuildJob(scope);
        var scheduler = await CreateInMemorySchedulerAsync();
        await job.Execute(new FakeJobExecutionContext(scheduler));

        var refreshedCache = await db.PracticeActivityCache.AsNoTracking().SingleAsync(c => c.Id == cache.Id);
        Assert.Equal(PracticeCacheStatus.Ready, refreshedCache.Status);
        Assert.NotNull(refreshedCache.LearningActivityId);

        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == refreshedCache.LearningActivityId);
        Assert.False(string.IsNullOrWhiteSpace(activity.FormIoSchemaJson));
        Assert.False(string.IsNullOrWhiteSpace(activity.ScoringRulesJson));
        Assert.DoesNotContain("correctAnswer", activity.FormIoSchemaJson, StringComparison.OrdinalIgnoreCase);
        await scheduler.Shutdown();

        var readinessItem = await db.StudentActivityReadinessItems.AsNoTracking()
            .SingleAsync(i => i.LearningActivityId == activity.Id);
        Assert.NotNull(readinessItem.SourceTemplateId);
    }

    [Fact]
    public async Task MigratedPattern_C3_ReorderParagraphs_MaterializesViaTemplatePath()
    {
        _factory.UseReorderParagraphsProvider();
        await _factory.EnsureCreatedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        await EnablePilotFlagAsync(db);
        var profileId = await SeedStudentAsync(db);

        var cache = new PracticeActivityCache(
            profileId, "reorder_paragraphs", "B1", "general_workplace",
            contentFingerprint: Guid.NewGuid().ToString("N"));
        db.PracticeActivityCache.Add(cache);
        await db.SaveChangesAsync();

        var job = BuildJob(scope);
        var scheduler = await CreateInMemorySchedulerAsync();
        await job.Execute(new FakeJobExecutionContext(scheduler));

        var refreshedCache = await db.PracticeActivityCache.AsNoTracking().SingleAsync(c => c.Id == cache.Id);
        Assert.Equal(PracticeCacheStatus.Ready, refreshedCache.Status);
        Assert.NotNull(refreshedCache.LearningActivityId);

        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == refreshedCache.LearningActivityId);
        Assert.False(string.IsNullOrWhiteSpace(activity.FormIoSchemaJson));
        Assert.False(string.IsNullOrWhiteSpace(activity.ScoringRulesJson));
        Assert.DoesNotContain("correctAnswer", activity.FormIoSchemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctOrder", activity.FormIoSchemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ordered_sequence", activity.ScoringRulesJson, StringComparison.OrdinalIgnoreCase);
        await scheduler.Shutdown();

        var readinessItem = await db.StudentActivityReadinessItems.AsNoTracking()
            .SingleAsync(i => i.LearningActivityId == activity.Id);
        Assert.NotNull(readinessItem.SourceTemplateId);
    }

    [Fact]
    public async Task NonMigratedPattern_StillUsesLegacyGeneration()
    {
        _factory.UseReadingMcqProvider();
        await _factory.EnsureCreatedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        await EnablePilotFlagAsync(db);
        var profileId = await SeedStudentAsync(db);

        // "email_reply" is NOT in PracticeGymGenerationJob.TemplateMigratedPatternKeys.
        var cache = new PracticeActivityCache(
            profileId, "email_reply", "B1", "general_workplace",
            contentFingerprint: Guid.NewGuid().ToString("N"));
        db.PracticeActivityCache.Add(cache);
        await db.SaveChangesAsync();

        var job = BuildJob(scope, out var captureLogger);
        var scheduler = await CreateInMemorySchedulerAsync();
        await job.Execute(new FakeJobExecutionContext(scheduler));

        var refreshedCache = await db.PracticeActivityCache.AsNoTracking().SingleAsync(c => c.Id == cache.Id);
        Assert.True(refreshedCache.Status == PracticeCacheStatus.Ready, captureLogger.LastException?.ToString() ?? "no exception captured");
        Assert.NotNull(refreshedCache.LearningActivityId);

        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == refreshedCache.LearningActivityId);
        Assert.Null(activity.FormIoSchemaJson);
        Assert.False(string.IsNullOrWhiteSpace(activity.AiGeneratedContentJson));
        await scheduler.Shutdown();
    }

    // --- helpers ---

    private PracticeGymGenerationJob BuildJob(IServiceScope scope) => BuildJob(scope, out _);

    private PracticeGymGenerationJob BuildJob(IServiceScope scope, out CapturingLogger<PracticeGymGenerationJob> captureLogger)
    {
        var sp = scope.ServiceProvider;
        captureLogger = new CapturingLogger<PracticeGymGenerationJob>();
        return new PracticeGymGenerationJob(
            sp.GetRequiredService<LinguaCoachDbContext>(),
            sp.GetRequiredService<IAiActivityGenerator>(),
            sp.GetRequiredService<IExercisePatternRepository>(),
            sp.GetRequiredService<StudentProgressService>(),
            sp.GetRequiredService<ListeningAudioService>(),
            sp.GetRequiredService<ILearningGoalContextResolver>(),
            sp.GetRequiredService<ICurriculumRoutingService>(),
            sp.GetRequiredService<IStudentMasteryEvaluationService>(),
            sp.GetRequiredService<IStudentActivityReadinessPoolService>(),
            sp.GetRequiredService<ILearningPlanService>(),
            sp.GetRequiredService<IPracticeGymFormIoTemplatePilotSettingsProvider>(),
            sp.GetRequiredService<IActivityTemplateInstanceGenerator>(),
            sp.GetRequiredService<IActivityNoveltyPolicy>(),
            sp.GetRequiredService<IActivityContentFingerprintService>(),
            captureLogger);
    }

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) LastException = exception;
        }
    }

    private static async Task EnablePilotFlagAsync(LinguaCoachDbContext db)
    {
        var alreadySet = await db.RuntimeSettingOverrides.AnyAsync(o => o.Key == "PracticeGymFormIoPilot.Enabled" && o.IsActive);
        if (alreadySet) return;

        db.RuntimeSettingOverrides.Add(new RuntimeSettingOverride(
            "PracticeGymFormIoPilot.Enabled", "true", "bool", Guid.NewGuid(), "test setup"));
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedStudentAsync(LinguaCoachDbContext db)
    {
        var user = new ApplicationUser
        {
            UserName = $"pg_tmpl_{Guid.NewGuid():N}@test.com",
            Email = $"pg_tmpl_{Guid.NewGuid():N}@test.com",
            Role = UserRole.Student,
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var profile = new StudentProfile(user.Id);
        profile.SetCefrLevel("B1");
        db.StudentProfiles.Add(profile);
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
        var factory = new Quartz.Impl.StdSchedulerFactory(props);
        return await factory.GetScheduler();
    }
}

/// <summary>Test factory with a swappable fake AI provider for Phase C1 template-generation tests.</summary>
public sealed class PracticeGymTemplateTestFactory : ApiTestFactory
{
    private readonly SwappableTemplateAiProvider _provider = new();

    public void UseReadingMcqProvider() => _provider.Inner = new ReadingMcqFakeAiProvider();
    public void UseReadingMultiProvider() => _provider.Inner = new ReadingMultiFakeAiProvider();
    public void UseReorderParagraphsProvider() => _provider.Inner = new ReorderParagraphsFakeAiProvider();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var providerDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiProvider));
            if (providerDescriptor is not null) services.Remove(providerDescriptor);
            services.AddSingleton<IAiProvider>(_provider);

            var resolverDescriptors = services.Where(d => d.ServiceType == typeof(IAiProviderResolver)).ToList();
            foreach (var d in resolverDescriptors) services.Remove(d);
            services.AddScoped<IAiProviderResolver>(sp => new FakeTemplateAiProviderResolver(sp.GetRequiredService<IAiProvider>()));
        });
    }
}

/// <summary>Delegates to a swappable inner provider so tests can change AI behavior per-call.</summary>
internal sealed class SwappableTemplateAiProvider : IAiProvider
{
    public IAiProvider Inner { get; set; } = new ReadingMcqFakeAiProvider();
    public string ProviderName => Inner.ProviderName;
    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default) => Inner.CompleteAsync(request, ct);
}

/// <summary>
/// Branches by prompt key: returns a valid, student-safe Form.io schema matching the seeded
/// "reading_mcq_workplace_seed_v1" template's component keys (reading_passage, answer) for the
/// ActivityTemplate instance-generation prompt, and a valid legacy ModuleStageSchema payload for
/// every other (freeform/legacy) generation prompt — so non-migrated patterns' legacy generation
/// path is unaffected by this fixture.
/// </summary>
internal sealed class ReadingMcqFakeAiProvider : IAiProvider
{
    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        var json = request.PromptKey == "activity_template_generate_instance"
            ? FormIoSchemaJson
            : ModuleStageSchemaJson;
        return Task.FromResult(new AiResponse(json, InputTokens: 250, OutputTokens: 120, CostUsd: 0.001m, "fake-model", ProviderName));
    }

    private const string FormIoSchemaJson = """
        {"display":"form","components":[
          {"type":"content","key":"reading_passage","input":false,"html":"<p>The support team resolved the ticket within two hours because the issue was clearly described and included screenshots.</p>"},
          {"type":"radio","key":"answer","label":"Why was the ticket resolved quickly?","values":[
            {"label":"The customer called twice","value":"A"},
            {"label":"The issue was clearly described with screenshots","value":"B"},
            {"label":"The team worked overtime","value":"C"}
          ]}
        ]}
        """;

    private const string ModuleStageSchemaJson = """
        {
          "schemaVersion": "module_stage_v1",
          "primarySkill": "writing",
          "learnContent": {
            "teachingTitle": "Replying to a workplace email",
            "explanation": "Use a polite, clear structure when replying to a colleague's email.",
            "keyPoints": ["Acknowledge the request", "State your response clearly"],
            "examples": [{ "phrase": "Thank you for reaching out", "meaning": "polite opener", "note": null }],
            "strategy": null,
            "commonMistakes": [],
            "sourceLanguageSupport": null
          },
          "practiceContent": {
            "instructions": "Reply to the email below.",
            "scenario": "A colleague asks for a status update.",
            "task": "Write a short, polite reply.",
            "exerciseData": {
              "prompt": "Reply to confirm the status update.",
              "incomingMessage": "Hi, could you give me a quick status update on the project?"
            }
          },
          "feedbackPlan": {
            "evaluationCriteria": ["clarity", "politeness"],
            "rubric": [],
            "feedbackFocus": null,
            "successCriteria": ["Clear reply"]
          }
        }
        """;
}

/// <summary>
/// Phase C2 — branches by prompt key like <see cref="ReadingMcqFakeAiProvider"/>, but returns a
/// schema matching the seeded "reading_mcq_multi_workplace_seed_v1" template's component keys
/// (reading_passage, answers — a "selectboxes" multi-select component) for the ActivityTemplate
/// instance-generation prompt.
/// </summary>
internal sealed class ReadingMultiFakeAiProvider : IAiProvider
{
    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        var json = request.PromptKey == "activity_template_generate_instance"
            ? FormIoSchemaJson
            : ModuleStageSchemaJson;
        return Task.FromResult(new AiResponse(json, InputTokens: 250, OutputTokens: 120, CostUsd: 0.001m, "fake-model", ProviderName));
    }

    private const string FormIoSchemaJson = """
        {"display":"form","components":[
          {"type":"content","key":"reading_passage","input":false,"html":"<p>The billing team closed thirty invoices this week. Twenty-two were paid on time, six were disputed, and two are still pending manager approval.</p>"},
          {"type":"selectboxes","key":"answers","label":"Select all statements supported by the passage","values":[
            {"label":"Most invoices were paid on time","value":"A"},
            {"label":"No invoices were disputed","value":"B"},
            {"label":"A few invoices need manager approval","value":"C"},
            {"label":"All invoices were closed this week","value":"D"}
          ]}
        ]}
        """;

    private const string ModuleStageSchemaJson = """
        {
          "schemaVersion": "module_stage_v1",
          "primarySkill": "writing",
          "learnContent": {
            "teachingTitle": "Replying to a workplace email",
            "explanation": "Use a polite, clear structure when replying to a colleague's email.",
            "keyPoints": ["Acknowledge the request", "State your response clearly"],
            "examples": [{ "phrase": "Thank you for reaching out", "meaning": "polite opener", "note": null }],
            "strategy": null,
            "commonMistakes": [],
            "sourceLanguageSupport": null
          },
          "practiceContent": {
            "instructions": "Reply to the email below.",
            "scenario": "A colleague asks for a status update.",
            "task": "Write a short, polite reply.",
            "exerciseData": {
              "prompt": "Reply to confirm the status update.",
              "incomingMessage": "Hi, could you give me a quick status update on the project?"
            }
          },
          "feedbackPlan": {
            "evaluationCriteria": ["clarity", "politeness"],
            "rubric": [],
            "feedbackFocus": null,
            "successCriteria": ["Clear reply"]
          }
        }
        """;
}

/// <summary>
/// Phase C3 — branches by prompt key like <see cref="ReadingMcqFakeAiProvider"/>, but returns a
/// schema matching the seeded "reorder_paragraphs_workplace_seed_v1" template's component keys
/// (instructions, paragraphs — a stock "datagrid" with reorder enabled) for the ActivityTemplate
/// instance-generation prompt. Row order below is intentionally shuffled, matching the "never
/// leak the correct order into the student-safe schema" convention the seed itself follows.
/// </summary>
internal sealed class ReorderParagraphsFakeAiProvider : IAiProvider
{
    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        var json = request.PromptKey == "activity_template_generate_instance"
            ? FormIoSchemaJson
            : ModuleStageSchemaJson;
        return Task.FromResult(new AiResponse(json, InputTokens: 250, OutputTokens: 120, CostUsd: 0.001m, "fake-model", ProviderName));
    }

    private const string FormIoSchemaJson = """
        {"display":"form","components":[
          {"type":"content","key":"instructions","input":false,"html":"<p>Drag the steps below into the correct order for onboarding a new team member.</p>"},
          {"type":"datagrid","key":"paragraphs","label":"Onboarding steps","reorder":true,"disableAddingRemovingRows":true,"components":[
            {"type":"hidden","key":"itemId","input":true,"clearOnHide":false},
            {"type":"textarea","key":"text","input":true,"disabled":true,"clearOnHide":false}
          ],"defaultValue":[
            {"itemId":"p4","text":"During the second week, the new hire completes their first small task under the mentor's guidance and receives feedback."},
            {"itemId":"p2","text":"On the first day, the manager gives a short welcome tour of the office and introduces the new hire to the immediate team."},
            {"itemId":"p1","text":"Before the new hire's start date, IT sets up their email account, laptop, and access to the shared project folders."},
            {"itemId":"p5","text":"At the 30-day mark, the manager holds a short check-in meeting to review progress and address any open questions."},
            {"itemId":"p3","text":"By the end of the first week, assign the new hire a mentor from the team who can answer day-to-day questions and check in regularly."}
          ]}
        ]}
        """;

    private const string ModuleStageSchemaJson = """
        {
          "schemaVersion": "module_stage_v1",
          "primarySkill": "writing",
          "learnContent": {
            "teachingTitle": "Replying to a workplace email",
            "explanation": "Use a polite, clear structure when replying to a colleague's email.",
            "keyPoints": ["Acknowledge the request", "State your response clearly"],
            "examples": [{ "phrase": "Thank you for reaching out", "meaning": "polite opener", "note": null }],
            "strategy": null,
            "commonMistakes": [],
            "sourceLanguageSupport": null
          },
          "practiceContent": {
            "instructions": "Reply to the email below.",
            "scenario": "A colleague asks for a status update.",
            "task": "Write a short, polite reply.",
            "exerciseData": {
              "prompt": "Reply to confirm the status update.",
              "incomingMessage": "Hi, could you give me a quick status update on the project?"
            }
          },
          "feedbackPlan": {
            "evaluationCriteria": ["clarity", "politeness"],
            "rubric": [],
            "feedbackFocus": null,
            "successCriteria": ["Clear reply"]
          }
        }
        """;
}

internal sealed class FakeTemplateAiProviderResolver : IAiProviderResolver
{
    private readonly IAiProvider _provider;

    public FakeTemplateAiProviderResolver(IAiProvider provider) => _provider = provider;

    public AiProviderPair ResolveLlm(string featureKey, string categoryKey)
        => new(new AiProviderSelection(_provider, _provider.ProviderName, "fake-model"), Fallback: null);

    public AiTtsProviderSelection ResolveTts(string featureKey, string categoryKey)
        => new("fake", "fake", "fake");
}
