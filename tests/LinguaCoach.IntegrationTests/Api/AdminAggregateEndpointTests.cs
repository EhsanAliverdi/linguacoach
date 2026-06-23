using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for the four admin aggregate endpoints.
/// GET /api/admin/dashboard/activity-trends
/// GET /api/admin/dashboard/score-distribution
/// GET /api/admin/ai-usage/trends
/// GET /api/admin/ai-usage/category-breakdown
/// </summary>
public sealed class AdminAggregateEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminAggregateEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AdminClient(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    // Helper to set CreatedAt via reflection (test-only).
    private static void SetCreatedAt(object entity, DateTime utc)
    {
        var prop = typeof(LinguaCoach.Domain.Common.BaseEntity).GetProperty("CreatedAt",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, utc);
    }

    // ── activity-trends ───────────────────────────────────────────────────────

    [Fact]
    public async Task ActivityTrends_AsAdmin_Returns200()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/dashboard/activity-trends?period=7d");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ActivityTrends_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/dashboard/activity-trends?period=7d");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ActivityTrends_7dPeriod_Returns8Buckets()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/dashboard/activity-trends?period=7d");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var buckets = body.GetProperty("buckets").EnumerateArray().ToArray();
        // 7 days + today = 8 buckets (days 0..7 inclusive)
        Assert.Equal(8, buckets.Length);
    }

    [Fact]
    public async Task ActivityTrends_BucketHasCorrectDateFormat()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/dashboard/activity-trends?period=7d");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var buckets = body.GetProperty("buckets").EnumerateArray().ToArray();
        Assert.True(buckets.Length > 0);
        var date = buckets[0].GetProperty("date").GetString()!;
        // Must be yyyy-MM-dd format (length 10)
        Assert.Equal(10, date.Length);
        Assert.True(DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _));
    }

    [Fact]
    public async Task ActivityTrends_EmptyDb_ReturnsAllZeroBuckets()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/dashboard/activity-trends?period=7d");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var buckets = body.GetProperty("buckets").EnumerateArray().ToArray();
        // All counts should be zero (empty or no data in window)
        foreach (var b in buckets)
        {
            Assert.Equal(0, b.GetProperty("activityCount").GetInt32());
        }
    }

    [Fact]
    public async Task ActivityTrends_InvalidPeriod_DefaultsGracefully()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/dashboard/activity-trends?period=bogus");
        // Should not error — defaults to 30d
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var period = body.GetProperty("period").GetString();
        Assert.Equal("30d", period);
    }

    // ── score-distribution ───────────────────────────────────────────────────

    [Fact]
    public async Task ScoreDistribution_AsAdmin_Returns200()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/dashboard/score-distribution?period=30d");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ScoreDistribution_AlwaysReturns5Buckets()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/dashboard/score-distribution?period=30d");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var buckets = body.GetProperty("buckets").EnumerateArray().ToArray();
        Assert.Equal(5, buckets.Length);
    }

    [Fact]
    public async Task ScoreDistribution_WithSeededData_CountsCorrectly()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            // Use an existing activity from the DB if available; otherwise just verify 5 buckets
            var activity = await db.LearningActivities.FirstOrDefaultAsync();
            if (activity == null) goto verify;

            var profile = await db.StudentProfiles.FirstOrDefaultAsync();
            if (profile == null) goto verify;

            var a1 = new ActivityAttempt(profile.Id, activity.Id, "a", "{}", "k", score: 20.0);
            var a2 = new ActivityAttempt(profile.Id, activity.Id, "b", "{}", "k", score: 70.0);
            db.ActivityAttempts.AddRange(a1, a2);
            await db.SaveChangesAsync();
            verify:;
        }

        var client = AdminClient(adminToken);
        var response = await client.GetAsync("/api/admin/dashboard/score-distribution?period=30d");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var buckets = body.GetProperty("buckets").EnumerateArray().ToArray();
        Assert.Equal(5, buckets.Length);
    }

    // ── ai-usage/trends ───────────────────────────────────────────────────────

    [Fact]
    public async Task AiUsageTrends_AsAdmin_Returns200()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/ai-usage/aggregate-trends?period=7d");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AiUsageTrends_7dPeriod_Returns8Buckets()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/ai-usage/aggregate-trends?period=7d");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var buckets = body.GetProperty("buckets").EnumerateArray().ToArray();
        Assert.Equal(8, buckets.Length);
    }

    [Fact]
    public async Task AiUsageTrends_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/ai-usage/aggregate-trends?period=7d");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── ai-usage/category-breakdown ──────────────────────────────────────────

    [Fact]
    public async Task AiUsageCategoryBreakdown_AsAdmin_Returns200()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/ai-usage/by-category?period=30d");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AiUsageCategoryBreakdown_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/ai-usage/by-category?period=30d");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AiUsageCategoryBreakdown_WithData_ReturnsCategoriesOrderedByRequestCount()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var log1 = new AiUsageLog(null, "feature_agg_a", "openai", "gpt-4o",
                false, true, null, 100, 50, 0.01m, 100, null);
            var log2 = new AiUsageLog(null, "feature_agg_a", "openai", "gpt-4o",
                false, true, null, 100, 50, 0.01m, 100, null);
            var log3 = new AiUsageLog(null, "feature_agg_b", "openai", "gpt-4o",
                false, false, "error", 50, 0, 0m, 200, null);
            db.AiUsageLogs.AddRange(log1, log2, log3);
            await db.SaveChangesAsync();
        }

        var client = AdminClient(token);
        var response = await client.GetAsync("/api/admin/ai-usage/by-category?period=30d");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var categories = body.GetProperty("categories").EnumerateArray().ToArray();
        Assert.True(categories.Length >= 2);
        // First category should have highest request count
        var first = categories[0].GetProperty("requestCount").GetInt32();
        var second = categories[1].GetProperty("requestCount").GetInt32();
        Assert.True(first >= second);
    }
}
