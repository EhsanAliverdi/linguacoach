using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 13A — Adaptive Placement Engine admin endpoints.
/// </summary>
public sealed class AdminPlacementEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminPlacementEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateStudentProfileAsync()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = CreateAdminClient(adminToken);
        var email = $"placement_test_{Guid.NewGuid():N}@test.com";
        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("studentProfileId").GetString()!);
    }

    // ── GET /api/admin/students/{id}/placement/latest ─────────────────────────

    [Fact]
    public async Task GetLatestPlacement_NoPlacement_ReturnsHasPlacementFalse()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();

        var client = CreateAdminClient(adminToken);
        var response = await client.GetAsync($"/api/admin/students/{studentId}/placement/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasPlacement").GetBoolean());
    }

    [Fact]
    public async Task GetLatestPlacement_UnknownStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = CreateAdminClient(adminToken);

        var response = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/placement/latest");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestPlacement_WithoutToken_Returns401()
    {
        var studentId = await CreateStudentProfileAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/admin/students/{studentId}/placement/latest");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /api/admin/students/{id}/placement/history ────────────────────────

    [Fact]
    public async Task GetPlacementHistory_NoHistory_ReturnsEmptyArray()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        var response = await client.GetAsync($"/api/admin/students/{studentId}/placement/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(0, body.GetArrayLength());
    }

    // ── POST /api/admin/students/{id}/placement/start ─────────────────────────

    [Fact]
    public async Task StartPlacement_NewStudent_Returns200WithAssessment()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        var response = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/start", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.Equal(studentId.ToString(), body.GetProperty("studentProfileId").GetString());
        Assert.True(body.GetProperty("itemCount").GetInt32() > 0);
    }

    [Fact]
    public async Task StartPlacement_Idempotent_ReturnsSameAssessment()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        var first = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/start", new { });
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var firstId = firstBody.GetProperty("assessmentId").GetString();

        var second = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/start", new { });
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        var secondId = secondBody.GetProperty("assessmentId").GetString();

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task StartPlacement_UnknownStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = CreateAdminClient(adminToken);

        var response = await client.PostAsJsonAsync(
            $"/api/admin/students/{Guid.NewGuid()}/placement/start", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── POST /api/admin/students/{id}/placement/{assessmentId}/complete ────────

    [Fact]
    public async Task CompletePlacement_AfterStart_Returns200WithResult()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        // Start
        var startResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/start", new { });
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        // Complete
        var completeResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/complete", new { });

        Assert.Equal(HttpStatusCode.OK, completeResp.StatusCode);
        var body = await completeResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", body.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("overallCefrLevel").GetString()));
    }

    [Fact]
    public async Task CompletePlacement_WrongStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var otherStudentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        // Start for one student
        var startResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/start", new { });
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        // Try to complete under a different student
        var completeResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{otherStudentId}/placement/{assessmentId}/complete", new { });

        Assert.Equal(HttpStatusCode.NotFound, completeResp.StatusCode);
    }

    [Fact]
    public async Task GetPlacementHistory_AfterStartAndComplete_ReturnsOneItem()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        var startResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/start", new { });
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/complete", new { });

        var historyResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/history");

        Assert.Equal(HttpStatusCode.OK, historyResp.StatusCode);
        var history = await historyResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, history.ValueKind);
        Assert.Equal(1, history.GetArrayLength());
        Assert.Equal("Completed", history[0].GetProperty("status").GetString());
    }
}
