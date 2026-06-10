using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Sessions;

/// <summary>
/// Phase 2 integration tests for the Exercise Pattern Engine.
///
/// Group A — DB-only tests (no HTTP): verify seeder and repository behaviour
///   using SQLite in-memory, one connection per test (xUnit creates a new instance per test).
///
/// Group B — HTTP endpoint tests (IClassFixture): verify that the prepare endpoint
///   creates activities with the correct ExercisePatternKey, and GET /api/activity/{id}
///   returns InteractionMode and ExercisePatternKey.
/// </summary>

// ── Group A: DB-only pattern tests ────────────────────────────────────────────

public sealed class ExercisePatternPhase2DbTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SqliteConnection _connection;

    public ExercisePatternPhase2DbTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.EnsureCreated();
        ExercisePatternSeeder.SeedAsync(_db, NullLogger.Instance).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── EmailReply pattern ────────────────────────────────────────────────────

    [Fact]
    public async Task EmailReply_Pattern_HasWritingScenarioActivityType()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.EmailReply);
        Assert.Equal(ActivityType.WritingScenario, pattern.ActivityType);
    }

    [Fact]
    public async Task EmailReply_Pattern_HasAiStructuredMarkingMode()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.EmailReply);
        Assert.Equal(MarkingMode.AiStructured, pattern.MarkingMode);
    }

    [Fact]
    public async Task EmailReply_Pattern_HasFreeTextEntryInteractionMode()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.EmailReply);
        Assert.Equal(InteractionMode.FreeTextEntry, pattern.InteractionMode);
    }

    [Fact]
    public async Task EmailReply_Pattern_IsCompatibleWithWritingTask()
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(ExerciseKind.WritingTask);
        Assert.Contains(results, p => p.Key == ExercisePatternKey.EmailReply);
    }

    // ── SpokenResponseFromPrompt pattern ──────────────────────────────────────

    [Fact]
    public async Task SpokenResponseFromPrompt_Pattern_HasSpeakingRolePlayActivityType()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.SpokenResponseFromPrompt);
        Assert.Equal(ActivityType.SpeakingRolePlay, pattern.ActivityType);
    }

    [Fact]
    public async Task SpokenResponseFromPrompt_Pattern_IsCompatibleWithSpeakingTask()
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(ExerciseKind.SpeakingTask);
        Assert.Contains(results, p => p.Key == ExercisePatternKey.SpokenResponseFromPrompt);
    }

    // ── All 8 patterns have non-empty prompt keys ─────────────────────────────

    [Theory]
    [InlineData(ExercisePatternKey.PhraseMatch)]
    [InlineData(ExercisePatternKey.GapFillWorkplacePhrase)]
    [InlineData(ExercisePatternKey.ListenAndAnswer)]
    [InlineData(ExercisePatternKey.ListenAndGapFill)]
    [InlineData(ExercisePatternKey.EmailReply)]
    [InlineData(ExercisePatternKey.TeamsChatSimulation)]
    [InlineData(ExercisePatternKey.SpokenResponseFromPrompt)]
    [InlineData(ExercisePatternKey.LessonReflection)]
    public async Task AllPatterns_HaveNonEmptyPromptKeys(string key)
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == key);
        Assert.False(string.IsNullOrWhiteSpace(pattern.AiGeneratePromptKey));
        Assert.False(string.IsNullOrWhiteSpace(pattern.AiEvaluatePromptKey));
    }

    // ── Template keys match seeded pattern keys ───────────────────────────────

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task TemplatePatternKeys_AllExistInSeededPatterns(int duration)
    {
        var seededKeys = await _db.ExercisePatterns.Select(p => p.Key).ToHashSetAsync();
        var steps = SessionDurationTemplates.GetTemplate(duration);
        foreach (var step in steps)
        {
            Assert.Contains(step.PatternKey, seededKeys);
        }
    }

    // ── ActivityGenerationContext carries OverridePromptKey ───────────────────

    [Fact]
    public async Task ActivityGenerationContext_WithOverridePromptKey_StoresEmailReplyKey()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.EmailReply);
        var ctx = new Application.Activity.ActivityGenerationContext(
            ActivityType: pattern.ActivityType,
            CefrLevel: "B1",
            CareerContext: "Tech",
            LanguagePairCode: "fa-en",
            SourceLanguageName: "Persian",
            TargetLanguageName: "English",
            OverridePromptKey: pattern.AiGeneratePromptKey,
            ExercisePatternKey: pattern.Key);

        Assert.Equal(pattern.AiGeneratePromptKey, ctx.OverridePromptKey);
        Assert.Equal(ExercisePatternKey.EmailReply, ctx.ExercisePatternKey);
    }

    // ── LearningActivity.ExercisePatternKey persists correctly ────────────────

    [Fact]
    public async Task LearningActivity_WithEmailReplyKey_RoundTripsCorrectly()
    {
        var activity = new Domain.Entities.LearningActivity(
            ActivityType.WritingScenario, ActivitySource.AiGenerated,
            "Email task", "B1", "{}", exercisePatternKey: ExercisePatternKey.EmailReply);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Equal(ExercisePatternKey.EmailReply, loaded.ExercisePatternKey);
    }

    [Fact]
    public async Task LearningActivity_WithSpokenResponseKey_RoundTripsCorrectly()
    {
        var activity = new Domain.Entities.LearningActivity(
            ActivityType.SpeakingRolePlay, ActivitySource.AiGenerated,
            "Speaking task", "B1", "{}", exercisePatternKey: ExercisePatternKey.SpokenResponseFromPrompt);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Equal(ExercisePatternKey.SpokenResponseFromPrompt, loaded.ExercisePatternKey);
    }

    [Fact]
    public async Task LearningActivity_LessonReflection_StoresPatternKey()
    {
        var activity = new Domain.Entities.LearningActivity(
            ActivityType.WritingScenario, ActivitySource.SystemFallback,
            "Reflection", "B1", "{}", exercisePatternKey: ExercisePatternKey.LessonReflection);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Equal(ExercisePatternKey.LessonReflection, loaded.ExercisePatternKey);
    }
}

// ── Group B: HTTP endpoint tests ──────────────────────────────────────────────

public sealed class ExercisePatternPhase2EndpointTests : IClassFixture<SessionTestFactory>
{
    private readonly SessionTestFactory _factory;

    public ExercisePatternPhase2EndpointTests(SessionTestFactory factory) => _factory = factory;

    // ── Prepare sets ExercisePatternKey on activity ───────────────────────────

    [Fact]
    public async Task Prepare_EmailReplyExercise_SetsExercisePatternKeyOnActivity()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"p2_epk_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var writingExercise = exercises.FirstOrDefault(e =>
            e.TryGetProperty("patternKey", out var pk) && pk.GetString() == ExercisePatternKey.EmailReply);

        if (writingExercise.ValueKind == JsonValueKind.Undefined)
            return; // Student preference doesn't include WritingTask with email_reply — skip.

        var exerciseId = writingExercise.GetProperty("exerciseId").GetGuid();
        var resp = await client.PostAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var activityId = (await resp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("activityId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var activity = await db.LearningActivities.FirstAsync(a => a.Id == activityId);
        Assert.Equal(ExercisePatternKey.EmailReply, activity.ExercisePatternKey);
    }

    [Fact]
    public async Task Prepare_ReviewExercise_SetsLessonReflectionPatternKey()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"p2_rev_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var reviewEx = exercises.FirstOrDefault(e =>
            e.GetProperty("kind").GetString() == "review");
        if (reviewEx.ValueKind == JsonValueKind.Undefined)
            return;

        var exerciseId = reviewEx.GetProperty("exerciseId").GetGuid();
        await client.PostAsync($"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var exercise = await db.SessionExercises.FirstAsync(e => e.Id == exerciseId);
        Assert.NotNull(exercise.LearningActivityId);
        var activity = await db.LearningActivities.FirstAsync(a => a.Id == exercise.LearningActivityId!.Value);
        Assert.Equal(ExercisePatternKey.LessonReflection, activity.ExercisePatternKey);
    }

    // ── Prepare idempotency still works with pattern key ─────────────────────

    [Fact]
    public async Task Prepare_CalledTwice_PatternKeyUnchanged()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"p2_idem_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var firstNonReview = exercises.First(e => e.GetProperty("kind").GetString() != "review");
        var exerciseId = firstNonReview.GetProperty("exerciseId").GetGuid();
        var url = $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare";

        var r1 = await client.PostAsync(url, null);
        var id1 = (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activityId").GetGuid();

        await client.PostAsync(url, null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var activity = await db.LearningActivities.FirstAsync(a => a.Id == id1);
        // Pattern key set on first call must remain after second call.
        // (May be null for vocab/legacy — that's fine, just must not change.)
        var patternKey = activity.ExercisePatternKey;

        // Call prepare a second time — verify the same activity is returned.
        var r2 = await client.PostAsync(url, null);
        var id2 = (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activityId").GetGuid();
        Assert.Equal(id1, id2);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var activityAfter = await db2.LearningActivities.FirstAsync(a => a.Id == id1);
        Assert.Equal(patternKey, activityAfter.ExercisePatternKey);
    }

    // ── GET /api/activity/{id} returns interactionMode and exercisePatternKey ─

    [Fact]
    public async Task GetActivity_ForPreparedExercise_ReturnsExercisePatternKeyInDto()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"p2_get_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var writingEx = exercises.FirstOrDefault(e =>
            e.TryGetProperty("patternKey", out var pk) && pk.GetString() == ExercisePatternKey.EmailReply);
        if (writingEx.ValueKind == JsonValueKind.Undefined)
            return;

        var exerciseId = writingEx.GetProperty("exerciseId").GetGuid();
        var prepResp = await client.PostAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare", null);
        var activityId = (await prepResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("activityId").GetGuid();

        var getResp = await client.GetAsync($"/api/activity/{activityId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var body = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var returnedPatternKey = body.TryGetProperty("exercisePatternKey", out var pk)
            ? pk.GetString() : null;
        Assert.Equal(ExercisePatternKey.EmailReply, returnedPatternKey);
    }

    [Fact]
    public async Task GetActivity_LegacyActivity_NullPatternKeyStillWorks()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"p2_legacy_{Guid.NewGuid():N}@test.com");

        // Create a legacy activity with no pattern key directly in DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var legacyActivity = new Domain.Entities.LearningActivity(
            ActivityType.WritingScenario, ActivitySource.SystemFallback,
            "Legacy writing task", "B1",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                title = "Legacy task",
                situation = "Old task with no pattern key",
                learningGoal = "Write professionally",
                targetPhrases = Array.Empty<string>(),
                targetVocabulary = Array.Empty<string>()
            }));
        db.LearningActivities.Add(legacyActivity);
        await db.SaveChangesAsync();

        Assert.Null(legacyActivity.ExercisePatternKey);

        var client = ClientWithToken(token);
        var getResp = await client.GetAsync($"/api/activity/{legacyActivity.Id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var body = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ActivityType.WritingScenario.ToString(),
            body.GetProperty("activityType").GetString(),
            ignoreCase: true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid SessionId, List<JsonElement> Exercises)> GetSessionWithExercisesAsync(
        HttpClient client)
    {
        var todayResp = await client.GetAsync("/api/sessions/today");
        var todayBody = await todayResp.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = todayBody.GetProperty("sessionId").GetGuid();

        var detailResp = await client.GetAsync($"/api/sessions/{sessionId}");
        var detailBody = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        var exercises = detailBody.GetProperty("exercises").EnumerateArray().ToList();
        return (sessionId, exercises);
    }
}
