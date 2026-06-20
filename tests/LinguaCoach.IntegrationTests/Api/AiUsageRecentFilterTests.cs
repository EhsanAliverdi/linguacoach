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
/// Tests for provider/model/featureKey/status filters on GET /api/admin/ai-usage/recent.
/// Status semantics:
///   success  = WasSuccessful AND NOT IsFallback
///   failed   = NOT WasSuccessful
///   fallback = IsFallback (may also be successful)
/// </summary>
public sealed class AiUsageRecentFilterTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    private static bool _seeded;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);

    // Known seed values for deterministic assertions
    private const string ProviderA = "openai";
    private const string ProviderB = "anthropic";
    private const string ModelA    = "gpt-4o-mini";
    private const string ModelB    = "claude-sonnet-filter-test";
    private const string FeatureA  = "filter_test_writing";
    private const string FeatureB  = "filter_test_lesson";

    public AiUsageRecentFilterTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AdminClient(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<string> SeedAndGetTokenAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await _seedLock.WaitAsync();
        try
        {
            if (_seeded) return token;

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            // Log 1: openai / gpt-4o-mini / filter_test_writing / success (no fallback)
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureA, ProviderA, ModelA,
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 10, outputTokens: 5, costUsd: 0.001m, durationMs: 50, correlationId: null));

            // Log 2: anthropic / claude-sonnet-filter-test / filter_test_lesson / failed
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureB, ProviderB, ModelB,
                isFallback: false, wasSuccessful: false, failureReason: "TimeoutException",
                inputTokens: 20, outputTokens: 0, costUsd: 0m, durationMs: 5000, correlationId: null));

            // Log 3: openai / gpt-4o-mini / filter_test_lesson / fallback + successful
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureB, ProviderA, ModelA,
                isFallback: true, wasSuccessful: true, failureReason: null,
                inputTokens: 15, outputTokens: 8, costUsd: 0.002m, durationMs: 100, correlationId: null));

            // Log 4: anthropic / claude-sonnet-filter-test / filter_test_writing / success (no fallback)
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureA, ProviderB, ModelB,
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 30, outputTokens: 15, costUsd: 0.003m, durationMs: 200, correlationId: null));

            await db.SaveChangesAsync();
            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }

        return token;
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);

    // ── provider filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_ProviderFilter_ReturnsOnlyMatchingProvider()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?provider={Encode(ProviderB)}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0, "Expected at least one anthropic log");
        Assert.All(items, i => Assert.Equal(ProviderB, i.GetProperty("provider").GetString()));
    }

    // ── model filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_ModelFilter_ReturnsOnlyMatchingModel()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?model={Encode(ModelB)}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0, "Expected at least one ModelB log");
        Assert.All(items, i => Assert.Equal(ModelB, i.GetProperty("model").GetString()));
    }

    // ── featureKey filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_FeatureKeyFilter_ReturnsOnlyMatchingFeature()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?featureKey={Encode(FeatureA)}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0, "Expected at least one FeatureA log");
        Assert.All(items, i => Assert.Equal(FeatureA, i.GetProperty("featureKey").GetString()));
    }

    // ── status filters ────────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_StatusSuccess_ReturnsOnlySuccessfulNonFallback()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/recent?status=success&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0, "Expected at least one success log");
        Assert.All(items, i =>
        {
            Assert.True(i.GetProperty("wasSuccessful").GetBoolean(), "wasSuccessful must be true");
            Assert.False(i.GetProperty("isFallback").GetBoolean(), "isFallback must be false for 'success'");
        });
    }

    [Fact]
    public async Task Recent_StatusFailed_ReturnsOnlyFailedCalls()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/recent?status=failed&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0, "Expected at least one failed log");
        Assert.All(items, i =>
            Assert.False(i.GetProperty("wasSuccessful").GetBoolean(), "wasSuccessful must be false"));
    }

    [Fact]
    public async Task Recent_StatusFallback_ReturnsOnlyFallbackCalls()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/recent?status=fallback&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0, "Expected at least one fallback log");
        Assert.All(items, i =>
            Assert.True(i.GetProperty("isFallback").GetBoolean(), "isFallback must be true"));
    }

    [Fact]
    public async Task Recent_InvalidStatus_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/recent?status=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out _), "Expected 'error' field in 400 response");
    }

    // ── combined filter + date range ──────────────────────────────────────────

    [Fact]
    public async Task Recent_ProviderFilter_And_DateFilter_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        var future = Encode(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"));
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?provider={Encode(ProviderA)}&from={future}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // from=2030 means no logs qualify — verifies filters compose correctly
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Recent_StatusFilter_And_DateFilter_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        var future = Encode(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"));
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?status=success&from={future}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
    }

    // ── combined filter + pagination ──────────────────────────────────────────

    [Fact]
    public async Task Recent_ProviderFilter_And_Pagination_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?provider={Encode(ProviderA)}&page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // pageSize=1 so items array has at most 1 entry
        Assert.True(body.GetProperty("items").GetArrayLength() <= 1);
        // totalCount reflects total matching provider, not just this page
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);

        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, i => Assert.Equal(ProviderA, i.GetProperty("provider").GetString()));
    }

    // ── combined provider + model ─────────────────────────────────────────────

    [Fact]
    public async Task Recent_ProviderAndModel_FiltersCompose()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?provider={Encode(ProviderA)}&model={Encode(ModelA)}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0, "Expected logs matching both ProviderA and ModelA");
        Assert.All(items, i =>
        {
            Assert.Equal(ProviderA, i.GetProperty("provider").GetString());
            Assert.Equal(ModelA,    i.GetProperty("model").GetString());
        });
    }
}
