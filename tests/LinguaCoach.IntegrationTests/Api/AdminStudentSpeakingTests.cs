using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for:
///   GET /api/admin/students/{studentProfileId}/speaking-attempts
///   GET /api/admin/students/{studentProfileId}/speaking-attempts/{attemptId}/audio
/// Phase 16E — speaking submission visibility.
/// </summary>
public sealed class AdminStudentSpeakingTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminStudentSpeakingTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    // ── Auth guards ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SpeakingAttempts_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/speaking-attempts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SpeakingAttempts_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"spkforbid_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);
        var response = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/speaking-attempts");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SpeakingAudio_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/admin/students/{Guid.NewGuid()}/speaking-attempts/{Guid.NewGuid()}/audio");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SpeakingAudio_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"spkaudio_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);
        var response = await client.GetAsync(
            $"/api/admin/students/{Guid.NewGuid()}/speaking-attempts/{Guid.NewGuid()}/audio");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Empty state for a student with no recordings ─────────────────────────

    [Fact]
    public async Task SpeakingAttempts_AsAdmin_ForStudentWithNoRecordings_ReturnsEmptyStatus()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"spkempty_{Guid.NewGuid():N}@t.com");
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoach.Persistence.LinguaCoachDbContext>();
        var profileId = await db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstAsync();

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var response = await client.GetAsync($"/api/admin/students/{profileId}/speaking-attempts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("status", out var status));
        Assert.Equal("Empty", status.GetString());
        Assert.True(body.TryGetProperty("attempts", out var attempts));
        Assert.Equal(JsonValueKind.Array, attempts.ValueKind);
        Assert.Equal(0, attempts.GetArrayLength());
    }

    // ── Unknown student returns safe response, not 500 ───────────────────────

    [Fact]
    public async Task SpeakingAttempts_AsAdmin_ForUnknownStudent_DoesNotReturn500()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var response = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/speaking-attempts");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.TryGetProperty("status", out _));
        }
    }

    // ── Response shape ───────────────────────────────────────────────────────

    [Fact]
    public async Task SpeakingAttempts_AsAdmin_ResponseDoesNotContainStoragePath()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"spkleak_{Guid.NewGuid():N}@t.com");
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoach.Persistence.LinguaCoachDbContext>();
        var profileId = await db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstAsync();

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var response = await client.GetAsync($"/api/admin/students/{profileId}/speaking-attempts");
        var raw = await response.Content.ReadAsStringAsync();

        // Storage path prefix must never appear in any response body.
        Assert.DoesNotContain("speaking-recordings/", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── Audio retrieval — not found paths ────────────────────────────────────

    [Fact]
    public async Task SpeakingAudio_AsAdmin_ForUnknownAttempt_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var response = await client.GetAsync(
            $"/api/admin/students/{Guid.NewGuid()}/speaking-attempts/{Guid.NewGuid()}/audio");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
