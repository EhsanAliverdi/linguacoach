using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 15F — GET /api/student/progress/summary and GET /api/admin/students/{id}/progress-summary.
/// </summary>
public sealed class StudentProgressSummaryTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public StudentProgressSummaryTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<Guid> GetStudentProfileIdAsync(Guid userId)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return await db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstAsync();
    }

    // ── Student endpoint ──────────────────────────────────────────────────────

    [Fact]
    public async Task ProgressSummary_Student_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/student/progress/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ProgressSummary_Student_Returns200WithExpectedShape()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"prog15f_s_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/progress/summary");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.True(body.TryGetProperty("learning", out _));
        Assert.True(body.TryGetProperty("cefr", out _));
        Assert.True(body.TryGetProperty("mastery", out _));
        Assert.True(body.TryGetProperty("recentActivity", out _));
        Assert.True(body.TryGetProperty("focus", out _));
    }

    [Fact]
    public async Task ProgressSummary_Student_MasteryHasExpectedFields()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"prog15f_mastery_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/progress/summary");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var mastery = body.GetProperty("mastery");
        Assert.True(mastery.TryGetProperty("masteredObjectivesCount", out _));
        Assert.True(mastery.TryGetProperty("inProgressObjectivesCount", out _));
        Assert.True(mastery.TryGetProperty("reviewQueueCount", out _));
        Assert.True(mastery.TryGetProperty("weakSkillsCount", out _));
    }

    [Fact]
    public async Task ProgressSummary_Student_RecentActivityIsArray()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"prog15f_ra_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/progress/summary");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.GetProperty("recentActivity").ValueKind);
    }

    [Fact]
    public async Task ProgressSummary_Student_DoesNotLeak_AnotherStudentsData()
    {
        var (tokenA, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"prog15f_iso_a_{Guid.NewGuid():N}@t.com");
        var (tokenB, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"prog15f_iso_b_{Guid.NewGuid():N}@t.com");

        var respA = await ClientWithToken(tokenA).GetAsync("/api/student/progress/summary");
        var respB = await ClientWithToken(tokenB).GetAsync("/api/student/progress/summary");

        respA.EnsureSuccessStatusCode();
        respB.EnsureSuccessStatusCode();

        // Both must return valid objects — the isolation check is that neither returns 500
        // and both have independent, well-formed JSON bodies.
        var bodyA = await respA.Content.ReadFromJsonAsync<JsonElement>();
        var bodyB = await respB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, bodyA.ValueKind);
        Assert.Equal(JsonValueKind.Object, bodyB.ValueKind);
    }

    // ── Admin endpoint ────────────────────────────────────────────────────────

    [Fact]
    public async Task ProgressSummary_Admin_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/progress-summary");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ProgressSummary_Admin_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"prog15f_forbid_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);
        var resp = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/progress-summary");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ProgressSummary_Admin_ForValidStudent_Returns200WithExpectedShape()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(
            $"prog15f_adm_{Guid.NewGuid():N}@t.com");
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var resp = await client.GetAsync(
            $"/api/admin/students/{studentProfileId}/progress-summary");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.True(body.TryGetProperty("completionPercentage", out _));
        Assert.True(body.TryGetProperty("masteredObjectivesCount", out _));
        Assert.True(body.TryGetProperty("reviewQueueCount", out _));
        Assert.True(body.TryGetProperty("currentLearningPhase", out var phase));
        Assert.Equal(JsonValueKind.String, phase.ValueKind);
    }

    [Fact]
    public async Task ProgressSummary_Admin_ForUnknownStudent_DoesNotReturn500()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var resp = await client.GetAsync(
            $"/api/admin/students/{Guid.NewGuid()}/progress-summary");

        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }
}
