using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// T-Sprint8: Real progress page — GET /api/progress endpoint.
/// Tests: auth guard, empty state, distinct-activity count, retry count,
/// score calculations, score trend order, skill profile, learning focus safety,
/// module progress, and student data isolation.
/// </summary>
public sealed class ProgressEndpointTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public ProgressEndpointTests(ActivityTestFactory factory) => _factory = factory;

    // ── Auth guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Progress_WithNoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Empty state ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Progress_WithNoAttempts_ReturnsEmptyStatsSafely()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prog_empty_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var summary = body.GetProperty("summary");

        Assert.Equal(0, summary.GetProperty("activitiesCompleted").GetInt32());
        Assert.Equal(0, summary.GetProperty("totalAttempts").GetInt32());
        Assert.Equal(0, summary.GetProperty("retryAttempts").GetInt32());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("averageScore").ValueKind);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("latestScore").ValueKind);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("bestScore").ValueKind);

        // Score trend should be empty array
        var trend = body.GetProperty("scoreTrend");
        Assert.Equal(JsonValueKind.Array, trend.ValueKind);
        Assert.Equal(0, trend.GetArrayLength());

        // Skill progress should have skills array (may be empty)
        var skillSection = body.GetProperty("skillProgress");
        Assert.Equal(JsonValueKind.Array, skillSection.GetProperty("skills").ValueKind);
        Assert.Equal(JsonValueKind.Array, skillSection.GetProperty("topStrengths").ValueKind);
        Assert.Equal(JsonValueKind.Array, skillSection.GetProperty("weakestSkills").ValueKind);

        // Module progress should be an array
        Assert.Equal(JsonValueKind.Array, body.GetProperty("moduleProgress").ValueKind);
    }

    // ── Distinct activity count ───────────────────────────────────────────────

    [Fact]
    public async Task Progress_RetriesOfSameActivity_CountAsOneCompleted()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prog_retry_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Get activity
        var nextResp = await client.GetAsync("/api/activity/next");
        if (nextResp.StatusCode != HttpStatusCode.OK) return;
        var nextBody = await nextResp.Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        // Submit same activity 3 times
        for (int i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
                new { submittedContent = $"Retry attempt {i + 1}. Dear Manager, following up on the pending approval." });
        }

        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var summary = body.GetProperty("summary");

        // 3 retries of same activity = 1 distinct completed
        Assert.Equal(1, summary.GetProperty("activitiesCompleted").GetInt32());
        Assert.Equal(3, summary.GetProperty("totalAttempts").GetInt32());
        Assert.Equal(2, summary.GetProperty("retryAttempts").GetInt32());
    }

    // ── Score calculations ────────────────────────────────────────────────────

    [Fact]
    public async Task Progress_AfterAttempts_ReturnsScoreStats()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prog_scores_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var nextResp = await client.GetAsync("/api/activity/next");
        if (nextResp.StatusCode != HttpStatusCode.OK) return;
        var nextBody = await nextResp.Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        // Submit two attempts
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Manager, I wanted to follow up on the document submitted last week." });
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Manager, I hope you are well. I wanted to follow up professionally." });

        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var summary = body.GetProperty("summary");

        // Stats should be present (FakeAiProvider always returns a score)
        Assert.True(summary.GetProperty("totalAttempts").GetInt32() >= 2);

        // Average, latest, and best scores should be numeric if scores were returned
        var avgKind = summary.GetProperty("averageScore").ValueKind;
        var latestKind = summary.GetProperty("latestScore").ValueKind;
        var bestKind = summary.GetProperty("bestScore").ValueKind;

        // If AI returned scores they should all be numbers; FakeAiProvider may return null
        if (avgKind != JsonValueKind.Null)
        {
            Assert.Equal(JsonValueKind.Number, avgKind);
            Assert.Equal(JsonValueKind.Number, latestKind);
            Assert.Equal(JsonValueKind.Number, bestKind);
        }
    }

    // ── Score trend order ─────────────────────────────────────────────────────

    [Fact]
    public async Task Progress_ScoreTrend_OrderedNewestFirst()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prog_trend_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var nextResp = await client.GetAsync("/api/activity/next");
        if (nextResp.StatusCode != HttpStatusCode.OK) return;
        var nextBody = await nextResp.Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        // Submit multiple attempts
        for (int i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
                new { submittedContent = $"Attempt {i + 1}: Dear Manager, following up professionally." });
        }

        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var trend = body.GetProperty("scoreTrend");

        if (trend.GetArrayLength() < 2) return; // not enough scored attempts to verify order

        // Dates should be descending (newest first)
        var dates = trend.EnumerateArray()
            .Select(t => DateTime.Parse(t.GetProperty("attemptDate").GetString()!))
            .ToList();

        for (int i = 1; i < dates.Count; i++)
        {
            Assert.True(dates[i - 1] >= dates[i],
                $"Score trend not ordered newest first at index {i}: {dates[i - 1]:O} < {dates[i]:O}");
        }
    }

    // ── Skill profile returned ────────────────────────────────────────────────

    [Fact]
    public async Task Progress_SkillProfileIncluded_NoRawJson()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prog_skill_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var skillSection = body.GetProperty("skillProgress");

        // Must be a proper object with typed arrays — not raw JSON strings
        Assert.Equal(JsonValueKind.Array, skillSection.GetProperty("skills").ValueKind);
        Assert.Equal(JsonValueKind.Array, skillSection.GetProperty("topStrengths").ValueKind);
        Assert.Equal(JsonValueKind.Array, skillSection.GetProperty("weakestSkills").ValueKind);

        // If skills exist, each has expected shape
        foreach (var skill in skillSection.GetProperty("skills").EnumerateArray())
        {
            Assert.True(skill.TryGetProperty("skillKey", out _), "skillKey missing");
            Assert.True(skill.TryGetProperty("skillLabel", out _), "skillLabel missing");
            Assert.True(skill.TryGetProperty("isWeak", out _), "isWeak missing");
        }
    }

    // ── Learning focus safety ─────────────────────────────────────────────────

    [Fact]
    public async Task Progress_LearningFocus_NotRawJson()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prog_focus_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var focusKind = body.GetProperty("learningFocus").ValueKind;

        // learningFocus is either null or an object with typed fields
        if (focusKind == JsonValueKind.Null) return; // no memory yet — acceptable

        Assert.Equal(JsonValueKind.Object, focusKind);
        var focus = body.GetProperty("learningFocus");
        Assert.True(focus.TryGetProperty("nextRecommendedFocus", out var nrf));
        Assert.Equal(JsonValueKind.Array, nrf.ValueKind);
        Assert.True(focus.TryGetProperty("recurringMistakes", out var rm));
        Assert.Equal(JsonValueKind.Array, rm.ValueKind);
    }

    // ── Module progress ───────────────────────────────────────────────────────

    [Fact]
    public async Task Progress_ModuleProgress_IncludedWithStatusField()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prog_mods_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Trigger path generation
        await client.GetAsync("/api/activity/next");

        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var modules = body.GetProperty("moduleProgress");
        Assert.Equal(JsonValueKind.Array, modules.ValueKind);

        foreach (var mod in modules.EnumerateArray())
        {
            Assert.True(mod.TryGetProperty("moduleId", out _), "moduleId missing");
            Assert.True(mod.TryGetProperty("title", out _), "title missing");
            Assert.True(mod.TryGetProperty("status", out var status), "status missing");

            var statusVal = status.GetString();
            Assert.True(
                statusVal is "completed" or "current" or "upcoming",
                $"Unexpected status value: {statusVal}");
        }
    }

    // ── Student isolation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Progress_DoesNotIncludeAnotherStudentsAttempts()
    {
        var (tokenA, _) = await _factory.CreateOnboardedStudentAsync($"prog_iso_a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateOnboardedStudentAsync($"prog_iso_b_{Guid.NewGuid():N}@test.com");

        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // Student A submits attempts
        var nextResp = await clientA.GetAsync("/api/activity/next");
        if (nextResp.StatusCode == HttpStatusCode.OK)
        {
            var nextBody = await nextResp.Content.ReadFromJsonAsync<JsonElement>();
            var activityId = nextBody.GetProperty("activityId").GetString()!;
            for (int i = 0; i < 3; i++)
            {
                await clientA.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
                    new { submittedContent = $"Student A attempt {i + 1}: Dear Manager, following up." });
            }
        }

        // Student B should see zero attempts
        var respB = await clientB.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);

        var bodyB = await respB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, bodyB.GetProperty("summary").GetProperty("totalAttempts").GetInt32());
    }

    // ── Score trend: activity titles returned ─────────────────────────────────

    [Fact]
    public async Task Progress_ScoreTrend_HasActivityTitles()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prog_titles_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var nextResp = await client.GetAsync("/api/activity/next");
        if (nextResp.StatusCode != HttpStatusCode.OK) return;
        var nextBody = await nextResp.Content.ReadFromJsonAsync<JsonElement>();
        var activityId = nextBody.GetProperty("activityId").GetString()!;

        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Manager, I hope you are well. I wanted to follow up on the approval." });

        var resp = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var trend = body.GetProperty("scoreTrend");

        foreach (var point in trend.EnumerateArray())
        {
            Assert.True(point.TryGetProperty("activityTitle", out var title), "activityTitle missing");
            Assert.False(string.IsNullOrWhiteSpace(title.GetString()), "activityTitle should not be empty");
            Assert.True(point.TryGetProperty("attemptNumber", out var num));
            Assert.True(num.GetInt32() >= 1, "attemptNumber should be >= 1");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
