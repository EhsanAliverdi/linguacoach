using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 14A — StudentPlacementController.
/// Uses PlacementTestFactory (deterministic scoring, no live AI).
/// </summary>
public sealed class StudentPlacementControllerTests : IClassFixture<PlacementTestFactory>
{
    private readonly PlacementTestFactory _factory;

    public StudentPlacementControllerTests(PlacementTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(string token, Guid userId, string assessmentId, Guid itemId)>
        StartedAssessmentAsync(string email)
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);

        var resp = await client.PostAsync("/api/student/placement/start", null);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = body.GetProperty("assessmentId").GetString()!;

        var nextResp = await client.GetAsync($"/api/student/placement/next?assessmentId={assessmentId}");
        nextResp.EnsureSuccessStatusCode();
        var nextBody = await nextResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = Guid.Parse(nextBody.GetProperty("itemId").GetString()!);

        return (token, userId, assessmentId, itemId);
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Config_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/student/placement/config");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetCurrent_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/student/placement/current");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Start_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/student/placement/start", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Config ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Config_WithToken_ReturnsExpectedFlags()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"sp_config_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/placement/config");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("placementRequiredBeforeLearning", out _));
        Assert.True(body.TryGetProperty("allowSkipPlacement", out _));
        Assert.True(body.TryGetProperty("allowPlacementRetake", out _));
        Assert.True(body.TryGetProperty("autoStartPlacement", out _));
    }

    // ── GET current ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrent_BeforeStart_ReturnsNoPlacement()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"sp_nocurrent_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/placement/current");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasPlacement").GetBoolean());
    }

    [Fact]
    public async Task GetCurrent_AfterStart_ReturnsAssessment()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"sp_current_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        await client.PostAsync("/api/student/placement/start", null);

        var resp = await client.GetAsync("/api/student/placement/current");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.True(body.TryGetProperty("assessmentId", out _));
    }

    // ── POST start ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_SetsLifecycleToPlacementInProgress()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"sp_start_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync("/api/student/placement/start", null);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InProgress", body.GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(StudentLifecycleStage.PlacementInProgress, profile.LifecycleStage);
    }

    [Fact]
    public async Task Start_WhenAlreadyCompleted_Returns409()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"sp_retake_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Start and complete the assessment
        var startResp = await client.PostAsync("/api/student/placement/start", null);
        startResp.EnsureSuccessStatusCode();
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });

        // Second start should return 409 (AllowPlacementRetake defaults to false)
        var resp = await client.PostAsync("/api/student/placement/start", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ── GET next ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNext_AfterStart_ReturnsAdaptiveItem()
    {
        var (token, _, assessmentId, _) = await StartedAssessmentAsync($"sp_next_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync($"/api/student/placement/next?assessmentId={assessmentId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("itemId", out _));
        Assert.True(body.TryGetProperty("prompt", out _));
        Assert.True(body.TryGetProperty("itemType", out _));
        Assert.True(body.TryGetProperty("skill", out _));
    }

    [Fact]
    public async Task GetNext_OtherStudentsAssessment_Returns404()
    {
        var (_, _, assessmentId, _) = await StartedAssessmentAsync($"sp_owner_a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateOnboardedStudentAsync($"sp_owner_b_{Guid.NewGuid():N}@test.com");
        var clientB = ClientWithToken(tokenB);

        var resp = await clientB.GetAsync($"/api/student/placement/next?assessmentId={assessmentId}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── POST respond ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Respond_WithValidAnswer_ReturnsSubmitResult()
    {
        var (token, _, assessmentId, itemId) = await StartedAssessmentAsync($"sp_respond_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync("/api/student/placement/respond", new
        {
            assessmentId = Guid.Parse(assessmentId),
            itemId,
            response = "A",
            durationSeconds = 5
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("isCorrect", out _));
        Assert.True(body.TryGetProperty("score", out _));
        Assert.True(body.TryGetProperty("assessmentComplete", out _));
    }

    [Fact]
    public async Task Respond_OtherStudentsAssessment_Returns404()
    {
        var (_, _, assessmentId, itemId) = await StartedAssessmentAsync($"sp_resp_owner_a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateOnboardedStudentAsync($"sp_resp_owner_b_{Guid.NewGuid():N}@test.com");
        var clientB = ClientWithToken(tokenB);

        var resp = await clientB.PostAsJsonAsync("/api/student/placement/respond", new
        {
            assessmentId = Guid.Parse(assessmentId),
            itemId,
            response = "A"
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Respond_EmptyResponse_Returns400()
    {
        var (token, _, assessmentId, itemId) = await StartedAssessmentAsync($"sp_respond_empty_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync("/api/student/placement/respond", new
        {
            assessmentId = Guid.Parse(assessmentId),
            itemId,
            response = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── POST complete ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Complete_TransitionsLifecycleToPlacementCompleted()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"sp_complete_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var startResp = await client.PostAsync("/api/student/placement/start", null);
        startResp.EnsureSuccessStatusCode();
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        var resp = await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", body.GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(StudentLifecycleStage.PlacementCompleted, profile.LifecycleStage);
    }

    [Fact]
    public async Task Complete_OtherStudentsAssessment_Returns404()
    {
        var (tokenA, _) = await _factory.CreateOnboardedStudentAsync($"sp_comp_a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateOnboardedStudentAsync($"sp_comp_b_{Guid.NewGuid():N}@test.com");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var startResp = await clientA.PostAsync("/api/student/placement/start", null);
        startResp.EnsureSuccessStatusCode();
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        var resp = await clientB.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── POST resume ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Resume_WhenNoAssessment_StartsNewAssessment()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"sp_resume_new_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync("/api/student/placement/resume", null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.True(body.TryGetProperty("assessmentId", out _));
    }

    [Fact]
    public async Task Resume_WhenInProgress_ReturnsExistingAssessment()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"sp_resume_existing_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var startResp = await client.PostAsync("/api/student/placement/start", null);
        startResp.EnsureSuccessStatusCode();
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var firstId = startBody.GetProperty("assessmentId").GetString()!;

        var resumeResp = await client.PostAsync("/api/student/placement/resume", null);
        Assert.Equal(HttpStatusCode.OK, resumeResp.StatusCode);
        var resumeBody = await resumeResp.Content.ReadFromJsonAsync<JsonElement>();

        // Should return the same in-progress assessment, not a new one
        Assert.Equal(firstId, resumeBody.GetProperty("assessmentId").GetString());
    }
}
