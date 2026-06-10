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
