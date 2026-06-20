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
/// Tests for column filters on GET /api/admin/ai-usage/summary.
/// Verifies that provider/model/featureKey/status/studentId filters apply to all
/// summary aggregations (totals, byProvider, byFeature, token counts, cost).
/// Status semantics match 10U-5:
///   success  = WasSuccessful AND NOT IsFallback
///   failed   = NOT WasSuccessful
///   fallback = IsFallback
/// StudentId uses real FK — must reference an actual student_profiles row.
/// </summary>
public sealed class AiUsageSummaryFilterTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    private static bool _seeded;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);
    private static Guid _studentProfileId;

    private const string ProviderA  = "openai";
    private const string ProviderB  = "anthropic";
    private const string ModelA     = "gpt-4o-summary-filter";
    private const string ModelB     = "claude-summary-filter";
    private const string FeatureA   = "summary_filter_writing";
    private const string FeatureB   = "summary_filter_lesson";

    public AiUsageSummaryFilterTests(ApiTestFactory factory)
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

            // Create real student so FK constraint is satisfied.
            await _factory.CreateStudentAndGetTokenAsync("student_sumfilter@test.linguacoach.com");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            _studentProfileId = await db.StudentProfiles
                .AsNoTracking()
                .Join(db.Users.Where(u => u.Email == "student_sumfilter@test.linguacoach.com"),
                    p => p.UserId, u => u.Id, (p, _) => p.Id)
                .FirstAsync();

            // Log 1: ProviderA / ModelA / FeatureA / success / student
            //   cost=0.001, input=10, output=5
            db.AiUsageLogs.Add(new AiUsageLog(_studentProfileId, FeatureA, ProviderA, ModelA,
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 10, outputTokens: 5, costUsd: 0.001m, durationMs: 50, correlationId: null));

            // Log 2: ProviderB / ModelB / FeatureB / failed / no student
            //   cost=0, input=20, output=0
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureB, ProviderB, ModelB,
                isFallback: false, wasSuccessful: false, failureReason: "Timeout",
                inputTokens: 20, outputTokens: 0, costUsd: 0m, durationMs: 5000, correlationId: null));

            // Log 3: ProviderA / ModelA / FeatureB / fallback+successful / no student
            //   cost=0.002, input=15, output=8
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureB, ProviderA, ModelA,
                isFallback: true, wasSuccessful: true, failureReason: null,
                inputTokens: 15, outputTokens: 8, costUsd: 0.002m, durationMs: 100, correlationId: null));

            // Log 4: ProviderB / ModelB / FeatureA / success / no student
            //   cost=0.003, input=30, output=15
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

    private static string Encode(string v) => Uri.EscapeDataString(v);

    // ── provider filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_ProviderFilter_ReturnsTotalsForThatProviderOnly()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?provider={ProviderA}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Logs 1 and 3 are ProviderA
        Assert.True(body.GetProperty("totalCalls").GetInt32() >= 2,
            "Expected >= 2 calls for ProviderA");
        var byProvider = body.GetProperty("byProvider").EnumerateArray().ToList();
        Assert.All(byProvider, p =>
            Assert.Equal(ProviderA, p.GetProperty("provider").GetString()));
    }

    [Fact]
    public async Task Summary_ProviderFilter_ExcludesOtherProviders()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?provider={ProviderA}");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var byProvider = body.GetProperty("byProvider").EnumerateArray().ToList();
        Assert.DoesNotContain(byProvider, p =>
            string.Equals(p.GetProperty("provider").GetString(), ProviderB, StringComparison.OrdinalIgnoreCase));
    }

    // ── model filter ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_ModelFilter_ReturnsTotalsForThatModelOnly()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?model={Encode(ModelB)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Logs 2 and 4 are ModelB
        Assert.True(body.GetProperty("totalCalls").GetInt32() >= 2,
            "Expected >= 2 calls for ModelB");
        var byProvider = body.GetProperty("byProvider").EnumerateArray().ToList();
        Assert.All(byProvider, p =>
            Assert.Equal(ProviderB, p.GetProperty("provider").GetString()));
    }

    // ── featureKey filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_FeatureKeyFilter_ReturnsTotalsForThatFeatureOnly()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?featureKey={FeatureA}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Logs 1 and 4 are FeatureA
        Assert.True(body.GetProperty("totalCalls").GetInt32() >= 2);
        var byFeature = body.GetProperty("byFeature").EnumerateArray().ToList();
        Assert.All(byFeature, f =>
            Assert.Equal(FeatureA, f.GetProperty("feature").GetString()));
    }

    // ── status filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_StatusSuccess_ReturnsOnlySuccessNonFallbackLogs()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/summary?status=success");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Logs 1 and 4 are WasSuccessful && !IsFallback
        Assert.True(body.GetProperty("totalCalls").GetInt32() >= 2);
        Assert.Equal(0, body.GetProperty("failedCalls").GetInt32());
        Assert.Equal(0, body.GetProperty("fallbackCalls").GetInt32());
    }

    [Fact]
    public async Task Summary_StatusFailed_ReturnsOnlyFailedLogs()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/summary?status=failed");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Log 2 only
        Assert.True(body.GetProperty("totalCalls").GetInt32() >= 1);
        Assert.Equal(0, body.GetProperty("successfulCalls").GetInt32());
    }

    [Fact]
    public async Task Summary_StatusFallback_ReturnsOnlyFallbackLogs()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/summary?status=fallback");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Log 3 only
        Assert.True(body.GetProperty("totalCalls").GetInt32() >= 1);
        Assert.True(body.GetProperty("fallbackCalls").GetInt32() >= 1);
    }

    [Fact]
    public async Task Summary_InvalidStatus_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/summary?status=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out _), "Expected 'error' field");
    }

    // ── studentId filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_StudentIdFilter_ReturnsTotalsForThatStudentOnly()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?studentId={_studentProfileId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Only Log 1 has the student
        Assert.True(body.GetProperty("totalCalls").GetInt32() >= 1);
    }

    [Fact]
    public async Task Summary_UnknownStudentId_ReturnsZeroTotals()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?studentId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalCalls").GetInt32());
    }

    [Fact]
    public async Task Summary_InvalidStudentId_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/summary?studentId=not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out _), "Expected 'error' field");
    }

    // ── token and cost totals ─────────────────────────────────────────────────

    [Fact]
    public async Task Summary_ProviderFilter_UpdatesTokenTotals()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?provider={ProviderA}");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // Logs 1+3: input=10+15=25, output=5+8=13
        Assert.True(body.GetProperty("totalInputTokens").GetInt64() >= 25);
        Assert.True(body.GetProperty("totalOutputTokens").GetInt64() >= 13);
        Assert.True(body.GetProperty("totalTokens").GetInt64() >= 38);
    }

    [Fact]
    public async Task Summary_ProviderFilter_UpdatesCostTotals()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?provider={ProviderA}");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // Logs 1+3: cost=0.001+0.002=0.003
        Assert.True(body.GetProperty("totalCostUsd").GetDecimal() >= 0.003m);
    }

    // ── combined date + studentId ─────────────────────────────────────────────

    [Fact]
    public async Task Summary_DateAndStudentId_Combined_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        var future = Encode(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"));
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/summary?studentId={_studentProfileId}&from={future}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalCalls").GetInt32());
    }
}
