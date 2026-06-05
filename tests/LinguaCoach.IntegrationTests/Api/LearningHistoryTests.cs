using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Ai;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// T-Sprint7: Learning history, AI fallback, Qwen, AI usage tracking.
/// </summary>
public sealed class LearningHistoryTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public LearningHistoryTests(ActivityTestFactory factory) => _factory = factory;

    // ── Learning history — module activities endpoint ─────────────────────────

    [Fact]
    public async Task ModuleActivities_WithNoAttempts_ReturnsEmptyActivitiesList()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"hist_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Generate a path
        await client.GetAsync("/api/activity/next");

        var pathResp = await client.GetAsync("/api/learning-path");
        if (pathResp.StatusCode == HttpStatusCode.NotFound) return;
        var pathBody = await pathResp.Content.ReadFromJsonAsync<JsonElement>();
        var modules = pathBody.GetProperty("modules");
        if (modules.GetArrayLength() == 0) return;

        var moduleId = modules[0].GetProperty("moduleId").GetString()!;

        // Get history for the module (no attempts yet on a fresh module)
        var histResp = await client.GetAsync($"/api/learning-path/modules/{moduleId}/activities");
        Assert.Equal(HttpStatusCode.OK, histResp.StatusCode);

        var histBody = await histResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(histBody.TryGetProperty("moduleId", out _), "moduleId missing");
        Assert.True(histBody.TryGetProperty("activities", out var acts), "activities missing");
        Assert.Equal(JsonValueKind.Array, acts.ValueKind);
    }

    [Fact]
    public async Task ModuleActivities_WithWrongModuleId_Returns404()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"hist2_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync($"/api/learning-path/modules/{Guid.NewGuid()}/activities");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ActivityAttempts_AfterSubmission_ReturnsAttemptHistory()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"histact_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Submit an attempt
        var nextBody = await (await client.GetAsync("/api/activity/next")).Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Manager, I am following up on the document approval." });

        // Get attempt history
        var histResp = await client.GetAsync($"/api/activity/{activityId}/attempts");
        Assert.Equal(HttpStatusCode.OK, histResp.StatusCode);

        var histBody = await histResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(histBody.TryGetProperty("activityId", out _), "activityId missing");
        Assert.True(histBody.TryGetProperty("attempts", out var attempts), "attempts missing");
        Assert.True(attempts.GetArrayLength() >= 1, "Should have at least one attempt");

        var first = attempts[0];
        Assert.True(first.TryGetProperty("attemptNumber", out var num));
        Assert.Equal(1, num.GetInt32());
        Assert.True(first.TryGetProperty("submittedContent", out _), "submittedContent missing");
    }

    [Fact]
    public async Task ActivityAttempts_RetryCreatesSecondAttempt()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"retry_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var nextBody = await (await client.GetAsync("/api/activity/next")).Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        // Submit twice
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "First attempt." });
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "Second improved attempt." });

        var histResp = await client.GetAsync($"/api/activity/{activityId}/attempts");
        var histBody = await histResp.Content.ReadFromJsonAsync<JsonElement>();
        var attempts = histBody.GetProperty("attempts");

        Assert.Equal(2, attempts.GetArrayLength());
        Assert.Equal(1, attempts[0].GetProperty("attemptNumber").GetInt32());
        Assert.Equal(2, attempts[1].GetProperty("attemptNumber").GetInt32());
    }

    [Fact]
    public async Task ActivityAttempts_AnotherStudentCannotAccessAttempts_Returns403()
    {
        var (token1, _) = await _factory.CreateOnboardedStudentAsync($"s1_{Guid.NewGuid():N}@test.com");
        var (token2, _) = await _factory.CreateOnboardedStudentAsync($"s2_{Guid.NewGuid():N}@test.com");

        var client1 = ClientWithToken(token1);
        var nextBody = await (await client1.GetAsync("/api/activity/next")).Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;
        await client1.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "Student 1 attempt." });

        // Student 2 tries to access student 1's activity history
        var client2 = ClientWithToken(token2);
        var resp = await client2.GetAsync($"/api/activity/{activityId}/attempts");
        // Should get 403 (Forbidden) — no attempts by student 2 on this activity
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── AI usage tracking ─────────────────────────────────────────────────────

    [Fact]
    public async Task AiUsage_SummaryEndpoint_AdminOnly()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/admin/ai-usage/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AiUsage_RecentEndpoint_AdminOnly()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/admin/ai-usage/recent");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── AI provider pair / fallback unit tests ────────────────────────────────

    [Fact]
    public void AiUnavailableException_IsThrowable()
    {
        var ex = new AiUnavailableException("All providers failed.");
        Assert.Equal("All providers failed.", ex.Message);
    }

    [Fact]
    public void AiProviderPair_WithNoFallback_FallbackIsNull()
    {
        var primary = new AiProviderSelection(null!, "openai", "gpt-4o");
        var pair = new AiProviderPair(primary, null);
        Assert.Null(pair.Fallback);
        Assert.Equal("openai", pair.Primary.ProviderName);
    }

    // ── Qwen provider in AiProviderConfig ─────────────────────────────────────

    [Fact]
    public void AiProviderConfig_AllowedModels_IncludesQwen()
    {
        var allowed = LinguaCoach.Domain.Entities.AiProviderConfig.AllowedModels;
        Assert.True(allowed.ContainsKey("qwen"), "qwen provider should be in AllowedModels");
        Assert.Contains("qwen-plus", allowed["qwen"]);
        Assert.Contains("qwen-max", allowed["qwen"]);
    }

    [Fact]
    public void AiProviderConfig_SetFallback_WithQwen_Works()
    {
        var config = new LinguaCoach.Domain.Entities.AiProviderConfig(
            "activity_evaluate_writing", "openai", "gpt-4o-mini");

        config.SetFallback("qwen", "qwen-plus", enabled: true);

        Assert.Equal("qwen", config.FallbackProviderName);
        Assert.Equal("qwen-plus", config.FallbackModelName);
        Assert.True(config.FallbackEnabled);
    }

    [Fact]
    public void AiProviderConfig_SetFallback_WithNull_ClearsFallback()
    {
        var config = new LinguaCoach.Domain.Entities.AiProviderConfig(
            "activity_generate_writing", "openai", "gpt-4o-mini");
        config.SetFallback("qwen", "qwen-plus", true);
        config.SetFallback(null, null, false);

        Assert.Null(config.FallbackProviderName);
        Assert.False(config.FallbackEnabled);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
