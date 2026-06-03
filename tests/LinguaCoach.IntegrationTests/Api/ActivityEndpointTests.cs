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
/// T14: AI unavailable → ActivityHandler returns a SystemFallback activity (200), does not throw 500.
/// </summary>
public sealed class ActivityFallbackTests : IClassFixture<ActivityFallbackTestFactory>
{
    private readonly ActivityFallbackTestFactory _factory;

    public ActivityFallbackTests(ActivityFallbackTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetNextActivity_WhenAiUnavailable_Returns200WithSystemFallback()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"activity_fallback_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/activity/next");

        // Must not be 500 even though AI is unavailable
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var activityId = body.GetProperty("activityId").GetString();
        Assert.False(string.IsNullOrEmpty(activityId));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));

        // Source must be SystemFallback (2 = SystemFallback in ActivitySource enum)
        var sourceEl = body.GetProperty("source");
        if (sourceEl.ValueKind == JsonValueKind.Number)
            Assert.Equal(2, sourceEl.GetInt32()); // ActivitySource.SystemFallback = 2
        else
            Assert.Contains("systemFallback", sourceEl.GetString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetNextActivity_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitAttempt_WhenAiEvaluationFails_Returns200WithEmptyFeedback()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"activity_submit_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Get next activity to get a valid activity ID
        var nextResponse = await client.GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
        var nextBody = await nextResponse.Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        // Submit attempt — AI evaluation will fail (fake provider throws), but attempt should be saved
        var submitResponse = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Manager, I am writing to follow up on the pending approval." });

        // Must return 200 even if AI evaluation fails (attempt saved with empty feedback)
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var submitBody = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(submitBody.GetProperty("attemptId").GetString()));

        // Verify attempt was persisted in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var attempt = db.ActivityAttempts.FirstOrDefault(a => a.StudentProfileId == profile.Id);
        Assert.NotNull(attempt);
        Assert.Contains("pending approval", attempt.SubmittedContent);
    }

    [Fact]
    public async Task SubmitAttempt_WithBlankContent_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"activity_blank_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var nextResponse = await client.GetAsync("/api/activity/next");
        var nextBody = await nextResponse.Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        var response = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
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
/// Test factory with a failing IAiActivityGenerator so we can test the SystemFallback path.
/// </summary>
public sealed class ActivityFallbackTestFactory : WritingExerciseTestFactory
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
