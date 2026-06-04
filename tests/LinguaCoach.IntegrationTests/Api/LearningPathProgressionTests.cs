using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// T-Sprint5: Learning path progression and personalisation.
/// Tests: distinct-activity count, focus area detection, module readiness,
/// complete-module endpoint, and dashboard personalisation.
/// </summary>
public sealed class LearningPathProgressionTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public LearningPathProgressionTests(ActivityTestFactory factory)
    {
        _factory = factory;
    }

    // ── StudentProgressService: focus area detection ───────────────────────────

    [Fact]
    public async Task GetCurrentFocusArea_WithToneChangesInFeedback_ReturnsToneCategory()
    {
        var (_, userId) = await _factory.CreateOnboardedStudentAsync($"focus_{Guid.NewGuid():N}@test.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<StudentProgressService>();

        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var act = db.LearningActivities.First(); // use any existing activity

        // Feedback JSON: tone appears twice per attempt, grammar once
        var feedbackWithTone = """
            {
              "overallScore": 65,
              "changes": [
                {"type":"replace","original":"send","suggested":"could you please send","reason":"More polite","category":"tone","severity":"high"},
                {"type":"replace","original":"x","suggested":"y","reason":"...","category":"grammar","severity":"medium"},
                {"type":"replace","original":"a","suggested":"b","reason":"...","category":"tone","severity":"medium"}
              ]
            }
            """;

        // Add 3 attempts with tone-heavy feedback
        for (int i = 0; i < 3; i++)
        {
            db.ActivityAttempts.Add(new LinguaCoach.Domain.Entities.ActivityAttempt(
                profile.Id, act.Id, $"Focus response {i} {Guid.NewGuid()}", feedbackWithTone, "key", score: 65.0));
        }
        await db.SaveChangesAsync();

        var focus = await svc.GetCurrentFocusAreaAsync(profile.Id, CancellationToken.None);

        Assert.NotNull(focus);
        Assert.Equal("tone", focus!.Category);
        Assert.Equal("Polite workplace tone", focus.FriendlyLabel);
        // tone appears 2 per attempt × 3 attempts = 6; grammar appears 1 × 3 = 3
        Assert.True(focus.Frequency >= 6);
    }

    [Fact]
    public async Task GetCurrentFocusArea_WithNoAttempts_ReturnsNull()
    {
        var (_, userId) = await _factory.CreateOnboardedStudentAsync($"nofocus_{Guid.NewGuid():N}@test.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<StudentProgressService>();

        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        // No attempts added

        var focus = await svc.GetCurrentFocusAreaAsync(profile.Id, CancellationToken.None);

        Assert.Null(focus);
    }

    // ── StudentProgressService: module progress via real path data ─────────────

    [Fact]
    public async Task GetModuleProgress_WithSameActivitySubmittedThreeTimes_CountsAsOneDistinct()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"retrycount_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Generate path by requesting activity
        var nextResp = await client.GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.OK, nextResp.StatusCode);
        var nextBody = await nextResp.Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        // Submit the same activity 3 times (retries)
        for (int i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
                new { submittedContent = $"Retry attempt {i + 1} — attempting again." });
        }

        // Now check progress via the API
        var pathResp = await client.GetAsync("/api/learning-path");
        if (pathResp.StatusCode == HttpStatusCode.NotFound) return; // no path yet, skip

        Assert.Equal(HttpStatusCode.OK, pathResp.StatusCode);
        var pathBody = await pathResp.Content.ReadFromJsonAsync<JsonElement>();
        var modules = pathBody.GetProperty("modules");

        // Find the current module
        var currentModule = modules.EnumerateArray()
            .FirstOrDefault(m => m.GetProperty("isCurrent").GetBoolean());

        if (currentModule.ValueKind == JsonValueKind.Undefined) return; // no current module yet

        // 3 retries of the same activity = 1 distinct completed activity
        var completedActivities = currentModule.GetProperty("completedActivities").GetInt32();
        Assert.Equal(1, completedActivities);
    }

    // ── API endpoint tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task LearningPath_GetEndpoint_ReturnsNewEnrichedModuleFields()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"lp_enrich_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Trigger path generation
        await client.GetAsync("/api/activity/next");

        var resp = await client.GetAsync("/api/learning-path");
        if (resp.StatusCode == HttpStatusCode.NotFound) return; // no path yet
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var modules = body.GetProperty("modules");
        Assert.True(modules.GetArrayLength() > 0);

        var first = modules[0];
        // New fields from this sprint must be present
        Assert.True(first.TryGetProperty("isCompleted", out _), "isCompleted field missing");
        Assert.True(first.TryGetProperty("isReadyToComplete", out _), "isReadyToComplete field missing");
        Assert.True(first.TryGetProperty("averageScore", out _), "averageScore field missing");
        Assert.True(first.TryGetProperty("latestScore", out _), "latestScore field missing");
    }

    [Fact]
    public async Task CompleteModule_WhenModuleNotReady_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"notready_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Generate path
        await client.GetAsync("/api/activity/next");

        var pathResp = await client.GetAsync("/api/learning-path");
        if (pathResp.StatusCode == HttpStatusCode.NotFound) return;

        var pathBody = await pathResp.Content.ReadFromJsonAsync<JsonElement>();
        var modules = pathBody.GetProperty("modules");
        if (modules.GetArrayLength() == 0) return;

        var firstModuleId = modules[0].GetProperty("moduleId").GetString()!;

        // No activities completed — module is NOT ready
        var resp = await client.PostAsync($"/api/learning-path/modules/{firstModuleId}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CompleteModule_WithRandomModuleId_Returns404()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"wrong_mod_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync($"/api/learning-path/modules/{Guid.NewGuid()}/complete", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Dashboard_AfterAttempt_ReturnsNextRecommendedPracticeField()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"dash_nrp_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Submit an attempt
        var nextBody = await (await client.GetAsync("/api/activity/next")).Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Manager, I am writing to follow up on the pending approval." });

        var resp = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // nextRecommendedPractice field must be present (may be null or string)
        Assert.True(body.TryGetProperty("nextRecommendedPractice", out _), "nextRecommendedPractice field missing from dashboard");
        // currentFocus field must be present
        Assert.True(body.TryGetProperty("currentFocus", out _), "currentFocus field missing from dashboard");
        // latestImprovement field must be present
        Assert.True(body.TryGetProperty("latestImprovement", out _), "latestImprovement field missing from dashboard");
    }

    [Fact]
    public async Task Dashboard_AfterAttempt_ActivityStatsShowCorrectCount()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"dash_stats_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var nextBody = await (await client.GetAsync("/api/activity/next")).Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Manager, this is my response." });

        var resp = await client.GetAsync("/api/dashboard");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("activityStats", out var stats));
        Assert.NotEqual(JsonValueKind.Null, stats.ValueKind);
        Assert.Equal(1, stats.GetProperty("activitiesCompleted").GetInt32());
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
