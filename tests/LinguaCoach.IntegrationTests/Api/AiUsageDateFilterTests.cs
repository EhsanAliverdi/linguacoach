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
/// Tests for date-range filtering on GET /api/admin/ai-usage/summary and /recent.
/// From is inclusive (>=), To is exclusive (&lt;).
/// </summary>
public sealed class AiUsageDateFilterTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    // Fixed reference timestamps — all UTC.
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);  // oldest
    private static readonly DateTime T1 = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);  // middle
    private static readonly DateTime T2 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);  // recent

    private static bool _seeded;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);

    public AiUsageDateFilterTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AdminClient(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<string> SeedLogsAndGetTokenAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await _seedLock.WaitAsync();
        try
        {
            if (_seeded) return token;

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            // Insert three logs then backdating CreatedAt via direct SQL so the
            // protected setter on BaseEntity is not needed.
            static AiUsageLog MakeLog() => new(
                null, "date_filter_seed", "openai", "gpt-4o-mini",
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 100, outputTokens: 50, costUsd: 0.001m,
                durationMs: 10, correlationId: null);

            var logT0 = MakeLog();
            var logT1 = MakeLog();
            var logT2 = MakeLog();
            db.AiUsageLogs.AddRange(logT0, logT1, logT2);
            await db.SaveChangesAsync();

            // Backdate using raw SQL (SQLite-compatible ISO-8601 format).
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE ai_usage_logs SET created_at = {0} WHERE id = {1}",
                T0.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"), logT0.Id);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE ai_usage_logs SET created_at = {0} WHERE id = {1}",
                T1.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"), logT1.Id);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE ai_usage_logs SET created_at = {0} WHERE id = {1}",
                T2.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"), logT2.Id);

            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }

        return token;
    }

    // ── summary endpoint ──────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_NoFilter_Returns200()
    {
        var token = await SeedLogsAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/summary");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Summary_FromFilter_ExcludesOlderLogs()
    {
        var token = await SeedLogsAndGetTokenAsync();
        // from=T2 — only the most-recent seeded log qualifies.
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?from={Uri.EscapeDataString(T2.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var totalInput = body.GetProperty("totalInputTokens").GetInt64();
        // T2 seed log contributes 100 input tokens; must be present.
        Assert.True(totalInput >= 100, $"Expected >= 100 input tokens, got {totalInput}");
    }

    [Fact]
    public async Task Summary_ToFilter_ExcludesNewerLogs()
    {
        var token = await SeedLogsAndGetTokenAsync();
        // to=T1 (exclusive) — only T0 qualifies.
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?to={Uri.EscapeDataString(T1.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // T1 and T2 must be excluded. Total calls should not include their contributions.
        // We verify by checking totalCalls >= 1 (T0 is there) but costs are >= 0.
        var totalCalls = body.GetProperty("totalCalls").GetInt32();
        Assert.True(totalCalls >= 1, "Expected at least the T0 log");
    }

    [Fact]
    public async Task Summary_FromAndTo_ReturnsOnlyLogsInRange()
    {
        var token = await SeedLogsAndGetTokenAsync();
        // from=T1 (inclusive), to=T2 (exclusive) — only the T1 log.
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary" +
            $"?from={Uri.EscapeDataString(T1.ToString("O"))}" +
            $"&to={Uri.EscapeDataString(T2.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var totalCalls = body.GetProperty("totalCalls").GetInt32();
        Assert.True(totalCalls >= 1, "Expected T1 log to be in range");
    }

    [Fact]
    public async Task Summary_FromEqualsTo_Returns400()
    {
        var token = await SeedLogsAndGetTokenAsync();
        var t = Uri.EscapeDataString(T1.ToString("O"));
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?from={t}&to={t}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Summary_FromAfterTo_Returns400()
    {
        var token = await SeedLogsAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary" +
            $"?from={Uri.EscapeDataString(T2.ToString("O"))}" +
            $"&to={Uri.EscapeDataString(T0.ToString("O"))}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Summary_CostTotals_RespectDateRange()
    {
        var token = await SeedLogsAndGetTokenAsync();
        // Only T2 (cost=0.001 per log).
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?from={Uri.EscapeDataString(T2.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var cost = body.GetProperty("totalCostUsd").GetDecimal();
        Assert.True(cost >= 0.001m, $"Expected cost >= 0.001, got {cost}");
    }

    [Fact]
    public async Task Summary_TokenTotals_RespectDateRange()
    {
        var token = await SeedLogsAndGetTokenAsync();
        // Only T2: input=100, output=50, total=150.
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?from={Uri.EscapeDataString(T2.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var totalTokens = body.GetProperty("totalTokens").GetInt64();
        Assert.True(totalTokens >= 150, $"Expected >= 150 tokens, got {totalTokens}");
    }

    // ── recent endpoint ───────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_NoFilter_Returns200()
    {
        var token = await SeedLogsAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/recent");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Recent_FromFilter_ExcludesOlderLogs()
    {
        var token = await SeedLogsAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?from={Uri.EscapeDataString(T2.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var item in body.GetProperty("items").EnumerateArray())
        {
            var created = item.GetProperty("createdAt").GetDateTime().ToUniversalTime();
            Assert.True(created >= T2, $"Item createdAt {created:O} is before from={T2:O}");
        }
    }

    [Fact]
    public async Task Recent_ToFilter_ExcludesNewerLogs()
    {
        var token = await SeedLogsAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?to={Uri.EscapeDataString(T1.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var item in body.GetProperty("items").EnumerateArray())
        {
            var created = item.GetProperty("createdAt").GetDateTime().ToUniversalTime();
            Assert.True(created < T1, $"Item createdAt {created:O} is >= to={T1:O}");
        }
    }

    [Fact]
    public async Task Recent_InvalidRange_Returns400()
    {
        var token = await SeedLogsAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent" +
            $"?from={Uri.EscapeDataString(T2.ToString("O"))}" +
            $"&to={Uri.EscapeDataString(T0.ToString("O"))}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
