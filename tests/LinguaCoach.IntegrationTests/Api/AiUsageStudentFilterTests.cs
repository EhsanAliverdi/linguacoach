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
/// Tests for studentId filter on GET /api/admin/ai-usage/recent.
/// AiUsageLog.StudentProfileId has a FK to student_profiles, so tests create
/// real student profiles before seeding logs.
/// Unknown studentId returns empty paged result (not 404).
/// Invalid (non-GUID) studentId returns 400.
/// </summary>
public sealed class AiUsageStudentFilterTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    private static bool _seeded;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);
    private static Guid _profileA;
    private static Guid _profileB;

    public AiUsageStudentFilterTests(ApiTestFactory factory)
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

            // Create two real student users so FK constraint is satisfied.
            await _factory.CreateStudentAndGetTokenAsync("student_a_usagefilter@test.linguacoach.com");
            await _factory.CreateStudentAndGetTokenAsync("student_b_usagefilter@test.linguacoach.com");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            // Resolve StudentProfile.Id (not the ApplicationUser.Id) for each student.
            var profileA = await db.StudentProfiles
                .AsNoTracking()
                .Where(p => p.UserId != Guid.Empty)
                .Join(db.Users.Where(u => u.Email == "student_a_usagefilter@test.linguacoach.com"),
                    p => p.UserId, u => u.Id, (p, _) => p)
                .FirstAsync();

            var profileB = await db.StudentProfiles
                .AsNoTracking()
                .Join(db.Users.Where(u => u.Email == "student_b_usagefilter@test.linguacoach.com"),
                    p => p.UserId, u => u.Id, (p, _) => p)
                .FirstAsync();

            _profileA = profileA.Id;
            _profileB = profileB.Id;

            // 2 logs for student A (one success, one failed)
            db.AiUsageLogs.Add(new AiUsageLog(_profileA, "student_filter_test", "openai", "gpt-4o-mini",
                false, true, null, 10, 5, 0.001m, 50, null));
            db.AiUsageLogs.Add(new AiUsageLog(_profileA, "student_filter_test", "openai", "gpt-4o-mini",
                false, false, "TimeoutException", 10, 0, 0m, 5000, null));

            // 1 log for student B
            db.AiUsageLogs.Add(new AiUsageLog(_profileB, "student_filter_test", "anthropic", "claude-sonnet-4-6",
                false, true, null, 20, 10, 0.002m, 100, null));

            // 1 system log (no student)
            db.AiUsageLogs.Add(new AiUsageLog(null, "student_filter_test", "openai", "gpt-4o-mini",
                false, true, null, 5, 2, 0.0005m, 30, null));

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

    // ── studentId filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_StudentIdFilter_ReturnsOnlyThatStudentsLogs()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?studentId={_profileA}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.True(items.Count >= 2, $"Expected >= 2 logs for student A, got {items.Count}");
        Assert.All(items, i =>
            Assert.Equal(_profileA.ToString(), i.GetProperty("studentProfileId").GetString(),
                StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Recent_StudentIdFilter_ExcludesOtherStudents()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?studentId={_profileA}&pageSize=100");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.All(items, i =>
            Assert.NotEqual(_profileB.ToString(), i.GetProperty("studentProfileId").GetString(),
                StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Recent_UnknownStudentId_ReturnsEmptyPagedResult()
    {
        var token = await SeedAndGetTokenAsync();
        var unknownId = Guid.NewGuid();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?studentId={unknownId}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Recent_InvalidStudentId_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/recent?studentId=not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out _), "Expected 'error' field");
    }

    // ── studentId + date range ────────────────────────────────────────────────

    [Fact]
    public async Task Recent_StudentId_And_DateFilter_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        var future = Encode(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"));
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?studentId={_profileA}&from={future}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
    }

    // ── studentId + status filter ─────────────────────────────────────────────

    [Fact]
    public async Task Recent_StudentId_And_StatusFilter_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?studentId={_profileA}&status=failed&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.True(items.Count >= 1, "Expected at least one failed log for student A");
        Assert.All(items, i =>
        {
            Assert.False(i.GetProperty("wasSuccessful").GetBoolean());
            Assert.Equal(_profileA.ToString(),
                i.GetProperty("studentProfileId").GetString(), StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Recent_StudentId_And_ProviderFilter_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        // Student B only has anthropic logs — filter by openai should return 0
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?studentId={_profileB}&provider=openai&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
    }

    // ── studentId + pagination ────────────────────────────────────────────────

    [Fact]
    public async Task Recent_StudentId_And_Pagination_WorkTogether()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/recent?studentId={_profileA}&page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 2,
            "totalCount should reflect all student A logs, not just page 1");
    }
}
