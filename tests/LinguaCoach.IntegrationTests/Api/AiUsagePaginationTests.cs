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
/// Tests for page/pageSize pagination on GET /api/admin/ai-usage/recent.
/// Summary endpoint totals must remain independent from recent-call pagination.
/// </summary>
public sealed class AiUsagePaginationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    // 30 logs seeded so we have multiple pages at pageSize=10.
    private const int SeedCount = 30;

    private static bool _seeded;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);

    public AiUsagePaginationTests(ApiTestFactory factory)
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

    private async Task<string> SeedAndGetTokenAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await _seedLock.WaitAsync();
        try
        {
            if (_seeded) return token;

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            var logs = Enumerable.Range(0, SeedCount).Select(i => new AiUsageLog(
                null, "pagination_seed", "openai", "gpt-4o-mini",
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 10, outputTokens: 5, costUsd: 0.0001m,
                durationMs: 5, correlationId: null)).ToList();

            db.AiUsageLogs.AddRange(logs);
            await db.SaveChangesAsync();

            // Spread created_at by 1 second each so ordering is deterministic.
            var base_ = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < logs.Count; i++)
            {
                var ts = base_.AddSeconds(i).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE ai_usage_logs SET created_at = {0} WHERE id = {1}", ts, logs[i].Id);
            }

            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }

        return token;
    }

    // ── default pagination ────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_DefaultParams_Returns200WithPagedEnvelope()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/recent");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _), "missing 'items'");
        Assert.True(body.TryGetProperty("totalCount", out _), "missing 'totalCount'");
        Assert.True(body.TryGetProperty("page", out _), "missing 'page'");
        Assert.True(body.TryGetProperty("pageSize", out _), "missing 'pageSize'");
        Assert.True(body.TryGetProperty("totalPages", out _), "missing 'totalPages'");
    }

    [Fact]
    public async Task Recent_DefaultPageSize_Returns25Items()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/recent");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var items = body.GetProperty("items").GetArrayLength();
        var pageSize = body.GetProperty("pageSize").GetInt32();
        Assert.Equal(25, pageSize);
        Assert.True(items <= 25, $"Expected <= 25 items on default page, got {items}");
    }

    [Fact]
    public async Task Recent_Page1_AndPage2_ReturnDifferentItems()
    {
        var token = await SeedAndGetTokenAsync();
        var client = AdminClient(token);

        var r1 = await client.GetAsync("/api/admin/ai-usage/recent?page=1&pageSize=10");
        var r2 = await client.GetAsync("/api/admin/ai-usage/recent?page=2&pageSize=10");

        var b1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        var b2 = await r2.Content.ReadFromJsonAsync<JsonElement>();

        var ids1 = b1.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetString()).ToHashSet();
        var ids2 = b2.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetString()).ToHashSet();

        Assert.True(ids1.Count > 0, "page 1 returned no items");
        Assert.True(ids2.Count > 0, "page 2 returned no items");
        Assert.Empty(ids1.Intersect(ids2));
    }

    [Fact]
    public async Task Recent_TotalCountAndTotalPages_AreCorrect()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/recent?pageSize=10");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var totalCount = body.GetProperty("totalCount").GetInt32();
        var totalPages = body.GetProperty("totalPages").GetInt32();

        // We seeded SeedCount but other tests (date filter etc.) may also have added logs.
        Assert.True(totalCount >= SeedCount, $"Expected >= {SeedCount} total, got {totalCount}");
        Assert.True(totalPages >= (int)Math.Ceiling(totalCount / 10.0),
            $"totalPages {totalPages} inconsistent with totalCount {totalCount}");
    }

    // ── pageSize enforcement ──────────────────────────────────────────────────

    [Fact]
    public async Task Recent_PageSizeOver100_ClampedTo100()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/recent?pageSize=999");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var ps = body.GetProperty("pageSize").GetInt32();
        Assert.Equal(100, ps);
        Assert.True(body.GetProperty("items").GetArrayLength() <= 100);
    }

    [Fact]
    public async Task Recent_PageSizeZeroOrNegative_ClampedTo1()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/recent?pageSize=0");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Recent_PageBeyondTotal_ClampedToLastPage()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/recent?page=99999&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var page = body.GetProperty("page").GetInt32();
        var totalPages = body.GetProperty("totalPages").GetInt32();
        Assert.True(page <= totalPages, $"page {page} should be <= totalPages {totalPages}");
    }

    // ── ordering ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_ItemsReturnedNewestFirst()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/recent?pageSize=10");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var dates = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("createdAt").GetDateTime())
            .ToList();

        for (int i = 1; i < dates.Count; i++)
            Assert.True(dates[i - 1] >= dates[i],
                $"Items not newest-first at index {i}: {dates[i - 1]:O} < {dates[i]:O}");
    }

    // ── date filter + pagination together ────────────────────────────────────

    [Fact]
    public async Task Recent_DateFilter_And_Pagination_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        // Use a future date to get 0 results — verifies filter + pagination compose correctly.
        var future = Uri.EscapeDataString(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"));
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?from={future}&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    // ── summary totals independent from pagination ────────────────────────────

    [Fact]
    public async Task Summary_TotalCalls_NotLimitedByRecentPagination()
    {
        var token = await SeedAndGetTokenAsync();
        var client = AdminClient(token);

        // Summary with no filter — totalCalls must cover all seeded logs.
        var summaryResp = await client.GetAsync("/api/admin/ai-usage/summary");
        var summaryBody = await summaryResp.Content.ReadFromJsonAsync<JsonElement>();
        var totalCalls = summaryBody.GetProperty("totalCalls").GetInt32();

        // Recent page 1 at pageSize=1 returns only 1 item.
        var recentResp = await client.GetAsync("/api/admin/ai-usage/recent?pageSize=1");
        var recentBody = await recentResp.Content.ReadFromJsonAsync<JsonElement>();
        var recentItems = recentBody.GetProperty("items").GetArrayLength();

        Assert.Equal(1, recentItems);
        Assert.True(totalCalls >= SeedCount,
            $"Summary totalCalls {totalCalls} should be >= {SeedCount} regardless of recent pageSize");
    }
}
