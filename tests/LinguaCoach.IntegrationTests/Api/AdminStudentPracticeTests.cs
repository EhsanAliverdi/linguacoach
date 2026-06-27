using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for GET /api/admin/students/{studentId}/practice-summary
/// </summary>
public sealed class AdminStudentPracticeTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminStudentPracticeTests(ApiTestFactory factory)
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

    [Fact]
    public async Task PracticeSummary_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/practice-summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PracticeSummary_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"pracforbid_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);
        var response = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/practice-summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PracticeSummary_AsAdmin_ForValidStudent_Returns200WithExpectedShape()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"pracadmin_{Guid.NewGuid():N}@t.com");
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var response = await client.GetAsync($"/api/admin/students/{studentProfileId}/practice-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.True(body.TryGetProperty("status", out var status));
        Assert.Equal(JsonValueKind.String, status.ValueKind);
        Assert.True(body.TryGetProperty("reviewQueueCount", out _));
        Assert.True(body.TryGetProperty("reservedCount", out _));
        Assert.True(body.TryGetProperty("isReplenishmentRecommended", out _));
    }

    [Fact]
    public async Task PracticeSummary_AsAdmin_ForUnknownStudent_DoesNotReturn500()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var response = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/practice-summary");

        // The handler must not throw an unhandled 500 for an unknown student GUID.
        // A well-formed JSON response with a status string is acceptable regardless of value.
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.TryGetProperty("status", out var status));
            Assert.Equal(JsonValueKind.String, status.ValueKind);
        }
    }
}
