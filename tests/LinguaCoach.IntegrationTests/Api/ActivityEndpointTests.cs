using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// T14 (updated): AI unavailable → ActivityHandler returns 503 ServiceUnavailable (no fallback path).
/// Sprint removed SystemFallback; AI failure now surfaces as 503.
/// </summary>
public sealed class ActivityFallbackTests : IClassFixture<ActivityFallbackTestFactory>
{
    private readonly ActivityFallbackTestFactory _factory;

    public ActivityFallbackTests(ActivityFallbackTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetNextActivity_WhenAiUnavailable_Returns503()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"activity_fallback_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/activity/next");

        // No fallback: AI unavailable → 503 ServiceUnavailable
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GetNextActivity_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitAttempt_WhenAiEvaluationFails_SavesAttemptAndReturns200()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"activity_submit_{Guid.NewGuid():N}@test.com");

        // Seed a LearningActivity directly so we have a valid ID to submit against
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var scenario = db.WritingScenarios.First();
        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.SystemFallback,
            title: "Test activity",
            difficulty: "B1",
            aiGeneratedContentJson: """{"situation":"test","learningGoal":"test","targetPhrases":[],"targetVocabulary":[],"exampleText":"","commonMistakeToAvoid":"","instructionInSourceLanguage":""}""",
            sourceWritingScenarioId: scenario.Id);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        var client = ClientWithToken(token);
        var submitResponse = await client.PostAsJsonAsync(
            $"/api/activity/{activity.Id}/attempt",
            new { submittedContent = "Dear Manager, I am writing to follow up on the pending approval." });

        // ActivitySubmitHandler always saves the attempt and returns 200, even when AI evaluation fails.
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var body = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("attemptId").GetString()));

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoach.Persistence.LinguaCoachDbContext>();
        var profile = db2.StudentProfiles.First(p => p.UserId == userId);
        var attempt = db2.ActivityAttempts.FirstOrDefault(a => a.StudentProfileId == profile.Id);
        Assert.NotNull(attempt);
        Assert.Contains("pending approval", attempt.SubmittedContent);
    }

    [Fact]
    public async Task SubmitAttempt_WithBlankContent_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"activity_blank_{Guid.NewGuid():N}@test.com");

        // Seed a LearningActivity directly so we have a valid ID
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var scenario = db.WritingScenarios.First();
        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.SystemFallback,
            title: "Blank content test",
            difficulty: "B1",
            aiGeneratedContentJson: """{"situation":"test","learningGoal":"test","targetPhrases":[],"targetVocabulary":[],"exampleText":"","commonMistakeToAvoid":"","instructionInSourceLanguage":""}""",
            sourceWritingScenarioId: scenario.Id);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        var client = ClientWithToken(token);
        var response = await client.PostAsJsonAsync(
            $"/api/activity/{activity.Id}/attempt",
            new { submittedContent = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

/// <summary>
/// T-Sprint4: Structured feedback (changes list, coachSummary, miniLesson, etc.) and retry flow.
/// </summary>
public sealed class ActivityStructuredFeedbackTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public ActivityStructuredFeedbackTests(ActivityTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SubmitAttempt_TwiceForSameActivity_CreatesTwoSeparateAttempts()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"retry_{Guid.NewGuid():N}@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var nextBody = await (await client.GetAsync("/api/activity/next")).Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        // First attempt
        var resp1 = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear John please send me the document." });
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        var body1 = await resp1.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId1 = body1.GetProperty("attemptId").GetString()!;

        // Second attempt (same activity — improved version)
        var resp2 = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear John, could you please send me the document at your earliest convenience?" });
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var body2 = await resp2.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId2 = body2.GetProperty("attemptId").GetString()!;

        // Two different attempt IDs
        Assert.NotEqual(attemptId1, attemptId2);

        // Both attempts persisted in DB, neither overwrote the other
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var attempts = db.ActivityAttempts
            .Where(a => a.StudentProfileId == profile.Id && a.LearningActivityId == Guid.Parse(activityId))
            .OrderBy(a => a.CreatedAt)
            .ToList();

        Assert.Equal(2, attempts.Count);
        Assert.Contains(attempts, a => a.Id == Guid.Parse(attemptId1));
        Assert.Contains(attempts, a => a.Id == Guid.Parse(attemptId2));
        // First attempt data not overwritten
        Assert.Contains("please send me the document", attempts[0].SubmittedContent);
        Assert.Contains("earliest convenience", attempts[1].SubmittedContent);
    }

    [Fact]
    public async Task SubmitAttempt_WhenAiReturnsFocusFirst_FeedbackReflectsFlag()
    {
        // The FakeAiProvider returns focusFirst: false by default — this test
        // verifies the flag passes through correctly (value is false from fake).
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"focusfirst_{Guid.NewGuid():N}@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var nextBody = await (await client.GetAsync("/api/activity/next")).Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        var resp = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Test message for focus first check." });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("focusFirst", out var ff));
        Assert.Equal(JsonValueKind.False, ff.ValueKind); // fake provider returns false
    }
}

/// <summary>
/// Test factory with a failing IAiActivityGenerator so we can test the SystemFallback path.
/// </summary>
public sealed class ActivityFallbackTestFactory : ActivityTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder); // sets up SQLite, seeds writing scenarios, fake AI provider

        builder.ConfigureServices(services =>
        {
            // Replace IAiActivityGenerator with one that always throws, forcing SystemFallback.
            var descriptors = services.Where(d => d.ServiceType == typeof(IAiActivityGenerator)).ToList();
            foreach (var d in descriptors) services.Remove(d);
            services.AddScoped<IAiActivityGenerator, AlwaysFailingAiActivityGenerator>();
        });
    }

    /// <summary>
    /// Must seed SystemFallback LearningActivity rows from WritingScenarios so fallback works.
    /// </summary>
    public new async Task<(string Token, Guid UserId)> CreateOnboardedStudentAsync(
        string email = "activity_student@test.linguacoach.com")
    {
        var result = await base.CreateOnboardedStudentAsync(email);

        // Seed LearningActivity rows from WritingScenarios (simulates LearningActivitySeeder)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var scenarios = await db.WritingScenarios.Where(s => s.IsActive).ToListAsync();
        var existingSourceIds = await db.LearningActivities
            .Where(a => a.SourceWritingScenarioId != null)
            .Select(a => a.SourceWritingScenarioId!.Value)
            .ToHashSetAsync();

        foreach (var scenario in scenarios.Where(s => !existingSourceIds.Contains(s.Id)))
        {
            var content = System.Text.Json.JsonSerializer.Serialize(new
            {
                situation = scenario.Situation,
                learningGoal = scenario.LearningGoal,
                targetPhrases = new[] { "I wanted to follow up" },
                targetVocabulary = new[] { "pending", "approval" },
                exampleText = scenario.ExampleText,
                commonMistakeToAvoid = scenario.CommonMistakeToAvoid,
                instructionInSourceLanguage = ""
            });

            db.LearningActivities.Add(new LinguaCoach.Domain.Entities.LearningActivity(
                activityType: ActivityType.WritingScenario,
                source: ActivitySource.SystemFallback,
                title: scenario.Title,
                difficulty: scenario.Difficulty,
                aiGeneratedContentJson: content,
                sourceWritingScenarioId: scenario.Id));
        }

        await db.SaveChangesAsync();
        return result;
    }
}

/// <summary>Fake IAiActivityGenerator that always throws to force the SystemFallback code path.</summary>
internal sealed class AlwaysFailingAiActivityGenerator : IAiActivityGenerator
{
    public Task<string> GenerateActivityContentAsync(ActivityGenerationContext context, CancellationToken ct)
        => throw new InvalidOperationException("Simulated AI generation failure (test).");

    public Task<string> EvaluateAttemptAsync(ActivityEvaluationContext context, CancellationToken ct)
        => throw new InvalidOperationException("Simulated AI evaluation failure (test).");
}

/// <summary>
/// Verifies GET /api/activity/{id} exposes `stageContent.learn` for ListeningComprehension,
/// with the Learn step containing no audio/question/transcript content (the original bug),
/// for both module_stage_v1 rows and legacy flat rows via the compatibility adapter.
/// </summary>
public sealed class ActivityStageContentTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public ActivityStageContentTests(ActivityTestFactory factory)
    {
        _factory = factory;
    }

    private const string StagedListeningJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Voicemail practice",
      "moduleGoal": "Understand a workplace voicemail",
      "skillFocus": "listening",
      "exerciseType": "listening_comprehension",
      "learnContent": {
        "teachingTitle": "Listening for action and deadline",
        "explanation": "Listen for the main idea, the action requested, and any deadline.",
        "keyPoints": ["Focus on verbs", "Note any dates or times"],
        "examples": [{"phrase": "by end of day", "meaning": "before today finishes", "note": "common deadline phrase"}],
        "strategy": "Listen for who, what, and when.",
        "commonMistakes": ["Missing the deadline"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen and answer the questions.",
        "scenario": "A colleague leaves a voicemail.",
        "task": null,
        "exerciseData": {
          "speakerRole": "Manager",
          "listenerRole": "You",
          "audioScript": "Hi, please send me the report by 5pm today.",
          "transcriptAvailableAfterSubmit": true,
          "questions": [{"id": "q1", "question": "What was requested?", "expectedAnswer": "the report", "type": "short_answer"}],
          "responseTask": null
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Main idea understood"],
        "rubric": [{"criterion": "Main idea", "description": "Identifies the request", "weight": 1.0}],
        "feedbackFocus": "Main idea and deadline",
        "successCriteria": ["Identifies the requested action and deadline"]
      }
    }
    """;

    private const string LegacyFlatListeningJson = """
    {
      "scenario": "A colleague leaves a voicemail.",
      "speakerRole": "Manager",
      "listenerRole": "You",
      "instructions": "Listen and answer the questions.",
      "audioScript": "Hi, please send me the report by 5pm today.",
      "transcriptAvailableAfterSubmit": true,
      "questions": [{"id": "q1", "question": "What was requested?", "expectedAnswer": "the report", "type": "short_answer"}],
      "responseTask": null
    }
    """;

    private async Task<(HttpClient Client, Guid ActivityId)> SeedListeningActivityAsync(string contentJson, string title)
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"stagecontent_{Guid.NewGuid():N}@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            activityType: ActivityType.ListeningComprehension,
            source: ActivitySource.AiGenerated,
            title: title,
            difficulty: "B1",
            aiGeneratedContentJson: contentJson);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        return (client, activity.Id);
    }

    [Fact]
    public async Task GetById_WithModuleStageV1Content_ExposesStageContentWithoutExerciseDataInLearn()
    {
        var (client, activityId) = await SeedListeningActivityAsync(StagedListeningJson, "Voicemail practice");

        var response = await client.GetAsync($"/api/activity/{activityId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var stageContent = body.GetProperty("stageContent");
        Assert.Equal("module_stage_v1", stageContent.GetProperty("schemaVersion").GetString());

        var learn = stageContent.GetProperty("learn");
        Assert.Equal("Listening for action and deadline", learn.GetProperty("teachingTitle").GetString());
        Assert.True(learn.GetProperty("keyPoints").GetArrayLength() > 0);

        // The Learn step must never carry exercise content.
        var learnJson = learn.GetRawText();
        Assert.DoesNotContain("audioScript", learnJson);
        Assert.DoesNotContain("questions", learnJson);
        Assert.DoesNotContain("transcript", learnJson);

        var exerciseData = stageContent.GetProperty("practice").GetProperty("exerciseData");
        Assert.True(exerciseData.GetProperty("questions").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetById_WithLegacyFlatContent_AdaptsToLegacyAdaptedV1StageContent()
    {
        var (client, activityId) = await SeedListeningActivityAsync(LegacyFlatListeningJson, "Voicemail practice (legacy)");

        var response = await client.GetAsync($"/api/activity/{activityId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var stageContent = body.GetProperty("stageContent");
        Assert.Equal("legacy_adapted_v1", stageContent.GetProperty("schemaVersion").GetString());

        var learn = stageContent.GetProperty("learn");
        Assert.Equal("Voicemail practice (legacy)", learn.GetProperty("teachingTitle").GetString());

        // Old flat JSON passes through unchanged for Practice.
        var exerciseData = stageContent.GetProperty("practice").GetProperty("exerciseData");
        Assert.Equal("Hi, please send me the report by 5pm today.", exerciseData.GetProperty("audioScript").GetString());
    }
}
