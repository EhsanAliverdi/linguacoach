using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Tests for GET /api/admin/ai-usage/trends
/// Verifies daily buckets, filter application, zero-fill, token/cost aggregation,
/// and invalid-input 400s.
/// </summary>
public sealed class AiUsageTrendTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    private static bool _seeded;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);
    private static Guid _studentProfileId;

    // Unique identifiers to avoid cross-test interference
    private const string ProviderA  = "openai";
    private const string ProviderB  = "anthropic";
    private const string ModelA     = "gpt-4o-trend-test";
    private const string FeatureA   = "trend_test_writing";
    private const string FeatureB   = "trend_test_lesson";

    // Fixed past dates well outside any "today" window
    private static readonly DateTime Day1 = new(2025, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2025, 3, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3 = new(2025, 3, 13, 12, 0, 0, DateTimeKind.Utc); // gap on Day 12

    public AiUsageTrendTests(ApiTestFactory factory)
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

            await _factory.CreateStudentAndGetTokenAsync("student_trend@test.linguacoach.com");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            _studentProfileId = await db.StudentProfiles
                .AsNoTracking()
                .Join(db.Users.Where(u => u.Email == "student_trend@test.linguacoach.com"),
                    p => p.UserId, u => u.Id, (p, _) => p.Id)
                .FirstAsync();

            // Day1: 2 logs — ProviderA/success + ProviderB/failed
            var log1 = new AiUsageLog(_studentProfileId, FeatureA, ProviderA, ModelA,
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 100, outputTokens: 50, costUsd: 0.010m, durationMs: 100, correlationId: null);
            SetCreatedAt(log1, Day1);
            db.AiUsageLogs.Add(log1);

            var log2 = new AiUsageLog(null, FeatureB, ProviderB, "claude-trend",
                isFallback: false, wasSuccessful: false, failureReason: "Timeout",
                inputTokens: 200, outputTokens: 0, costUsd: 0m, durationMs: 5000, correlationId: null);
            SetCreatedAt(log2, Day1);
            db.AiUsageLogs.Add(log2);

            // Day2: 1 log — fallback + successful
            var log3 = new AiUsageLog(null, FeatureA, ProviderA, ModelA,
                isFallback: true, wasSuccessful: true, failureReason: null,
                inputTokens: 80, outputTokens: 40, costUsd: 0.008m, durationMs: 200, correlationId: null);
            SetCreatedAt(log3, Day2);
            db.AiUsageLogs.Add(log3);

            // Day3: 1 log — ProviderA/success (Day 12 is the gap)
            var log4 = new AiUsageLog(null, FeatureB, ProviderA, ModelA,
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 60, outputTokens: 30, costUsd: 0.006m, durationMs: 150, correlationId: null);
            SetCreatedAt(log4, Day3);
            db.AiUsageLogs.Add(log4);

            await db.SaveChangesAsync();
            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }

        return token;
    }

    // Override CreatedAt (protected set on BaseEntity) via reflection — test-only helper.
    private static void SetCreatedAt(AiUsageLog log, DateTime utc)
    {
        var prop = typeof(LinguaCoach.Domain.Common.BaseEntity).GetProperty("CreatedAt",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(log, utc);
    }

    private static string Encode(string v) => Uri.EscapeDataString(v);

    private static JsonElement[] Buckets(JsonElement body) =>
        body.EnumerateArray().ToArray();

    private static string RangeQuery(DateTime from, DateTime to) =>
        $"from={Encode(from.ToString("O"))}&to={Encode(to.ToString("O"))}";

    // ── basic response ────────────────────────────────────────────────────────

    [Fact]
    public async Task Trends_Returns200WithJsonArray()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day3.AddDays(1))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task Trends_ReturnsDailyBuckets_WithDateField()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day3.AddDays(1))}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        Assert.True(buckets.Length >= 3);
        Assert.All(buckets, b =>
        {
            var date = b.GetProperty("date").GetString();
            Assert.NotNull(date);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", date);
        });
    }

    // ── aggregation correctness ───────────────────────────────────────────────

    [Fact]
    public async Task Trends_Day1_HasCorrectCallCount()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day1.AddDays(1))}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        var day1 = buckets.FirstOrDefault(b => b.GetProperty("date").GetString() == "2025-03-10");
        Assert.True(day1.ValueKind != JsonValueKind.Undefined, "No bucket for 2025-03-10");
        Assert.True(day1.GetProperty("callCount").GetInt32() >= 2);
    }

    [Fact]
    public async Task Trends_Day1_TokenTotalsAggregate()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day1.AddDays(1))}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        var day1 = buckets.First(b => b.GetProperty("date").GetString() == "2025-03-10");
        // log1: in=100, out=50  log2: in=200, out=0  → total in>=300, out>=50
        Assert.True(day1.GetProperty("inputTokens").GetInt64() >= 300);
        Assert.True(day1.GetProperty("outputTokens").GetInt64() >= 50);
        Assert.True(day1.GetProperty("totalTokens").GetInt64() >= 350);
    }

    [Fact]
    public async Task Trends_Day1_CostTotalsAggregate()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day1.AddDays(1))}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        var day1 = buckets.First(b => b.GetProperty("date").GetString() == "2025-03-10");
        // log1: 0.010, log2: 0 → >=0.010
        Assert.True(day1.GetProperty("costUsd").GetDecimal() >= 0.010m);
    }

    [Fact]
    public async Task Trends_Day1_StatusCountsCorrect()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day1.AddDays(1))}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        var day1 = buckets.First(b => b.GetProperty("date").GetString() == "2025-03-10");
        Assert.True(day1.GetProperty("successCount").GetInt32() >= 1);  // log1
        Assert.True(day1.GetProperty("failureCount").GetInt32() >= 1);  // log2
        Assert.Equal(0, day1.GetProperty("fallbackCount").GetInt32());
    }

    [Fact]
    public async Task Trends_Day2_HasFallbackCount()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day2, Day2.AddDays(1))}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        var day2 = buckets.FirstOrDefault(b => b.GetProperty("date").GetString() == "2025-03-11");
        Assert.True(day2.ValueKind != JsonValueKind.Undefined);
        Assert.True(day2.GetProperty("fallbackCount").GetInt32() >= 1);
    }

    // ── zero-fill ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Trends_ZeroFillsMissingDay()
    {
        // Day1=10, Day2=11, Day3=13 → gap on Day 12 should be zero-filled
        var token = await SeedAndGetTokenAsync();
        var from = Day1;
        var to   = Day3.AddDays(1);
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(from, to)}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        var gap = buckets.FirstOrDefault(b => b.GetProperty("date").GetString() == "2025-03-12");
        Assert.True(gap.ValueKind != JsonValueKind.Undefined, "Expected zero-fill bucket for 2025-03-12");
        Assert.Equal(0, gap.GetProperty("callCount").GetInt32());
        Assert.Equal(0, gap.GetProperty("costUsd").GetDecimal());
    }

    // ── provider filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task Trends_ProviderFilter_ExcludesOtherProviders()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day1.AddDays(1))}&provider={ProviderA}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        var day1 = buckets.FirstOrDefault(b => b.GetProperty("date").GetString() == "2025-03-10");
        Assert.True(day1.ValueKind != JsonValueKind.Undefined);
        // Only log1 is ProviderA on Day1 → callCount should be 1 (not 2)
        Assert.Equal(1, day1.GetProperty("callCount").GetInt32());
    }

    // ── studentId filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task Trends_StudentIdFilter_OnlyIncludesThatStudent()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day1.AddDays(1))}&studentId={_studentProfileId}");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        // Only log1 has the student
        var total = buckets.Sum(b => b.GetProperty("callCount").GetInt32());
        Assert.True(total >= 1);
        Assert.True(total < 2); // log2 (no student) should be excluded
    }

    // ── status filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Trends_StatusFailed_OnlyIncludesFailedInBuckets()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(Day1, Day3.AddDays(1))}&status=failed");

        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        Assert.All(buckets, b =>
        {
            // All success/fallback counts must be 0 since filter restricts to failed rows
            Assert.Equal(0, b.GetProperty("successCount").GetInt32());
            Assert.Equal(0, b.GetProperty("fallbackCount").GetInt32());
        });
    }

    // ── date range boundary ───────────────────────────────────────────────────

    [Fact]
    public async Task Trends_FutureFrom_ReturnsEmptyArray()
    {
        var token = await SeedAndGetTokenAsync();
        var future = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(future, future.AddDays(7))}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var buckets = Buckets(await resp.Content.ReadFromJsonAsync<JsonElement>());
        Assert.Empty(buckets);
    }

    // ── invalid inputs ────────────────────────────────────────────────────────

    [Fact]
    public async Task Trends_InvalidStatus_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/trends?status=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Trends_InvalidStudentId_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/trends?studentId=not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Trends_InvertedDateRange_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var from = new DateTime(2025, 3, 13, 0, 0, 0, DateTimeKind.Utc);
        var to   = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/trends?{RangeQuery(from, to)}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
