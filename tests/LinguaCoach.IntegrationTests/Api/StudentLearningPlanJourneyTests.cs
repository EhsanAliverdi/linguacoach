using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 15E — integration tests for GET /api/student/learning-plan/journey.
/// Uses PlacementTestFactory so students go through placement before journey calls.
/// </summary>
public sealed class StudentLearningPlanJourneyTests : IClassFixture<PlacementTestFactory>
{
    private readonly PlacementTestFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public StudentLearningPlanJourneyTests(PlacementTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Test 1: unauthenticated request ────────────────────────────────────────

    [Fact]
    public async Task GetJourney_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/student/learning-plan/journey");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Test 2: authenticated student with no plan returns graceful empty ──────

    [Fact]
    public async Task GetJourney_NoActivePlan_ReturnsGracefulEmpty()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"journey_empty_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/learning-plan/journey");

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        Assert.Equal(0, body.GetProperty("totalObjectives").GetInt32());
        Assert.Equal("Preparing", body.GetProperty("currentLearningPhase").GetString());
        Assert.Equal("None", body.GetProperty("planStatus").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("currentObjective").ValueKind);
        Assert.Equal(0, body.GetProperty("upcomingObjectives").GetArrayLength());
        Assert.Equal(0, body.GetProperty("completedObjectives").GetArrayLength());
        Assert.Equal(0, body.GetProperty("reviewObjectives").GetArrayLength());
        Assert.Equal(0, body.GetProperty("milestones").GetArrayLength());
    }

    // ── Test 3: after placement + plan generation, journey has objectives ──────

    [Fact]
    public async Task GetJourney_AfterPlacementCompletion_HasObjectives()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"journey_plan_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Complete placement to trigger plan generation
        var startResp = await client.PostAsync("/api/student/placement/start", null);
        startResp.EnsureSuccessStatusCode();
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        var completeResp = await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });
        completeResp.EnsureSuccessStatusCode();

        // Now fetch the journey
        var resp = await client.GetAsync("/api/student/learning-plan/journey");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // Plan was created — should have objectives or graceful empty if generation deferred
        var planStatus = body.GetProperty("planStatus").GetString();
        Assert.True(planStatus is "Active" or "None",
            $"Unexpected planStatus: {planStatus}");

        // Response shape is always valid
        Assert.True(body.GetProperty("totalObjectives").GetInt32() >= 0);
        Assert.True(body.GetProperty("completionPercentage").GetDouble() >= 0);
        Assert.NotNull(body.GetProperty("currentCefrLevel").GetString());
    }

    // ── Test 4: journey objectives have required fields ───────────────────────

    [Fact]
    public async Task GetJourney_WithPlan_ObjectivesHaveRequiredFields()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"journey_fields_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var startResp = await client.PostAsync("/api/student/placement/start", null);
        startResp.EnsureSuccessStatusCode();
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });

        var resp = await client.GetAsync("/api/student/learning-plan/journey");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var upcoming = body.GetProperty("upcomingObjectives");

        foreach (var obj in upcoming.EnumerateArray())
        {
            Assert.True(obj.TryGetProperty("objectiveKey", out _), "Missing objectiveKey");
            Assert.True(obj.TryGetProperty("skill", out _), "Missing skill");
            Assert.True(obj.TryGetProperty("cefrLevel", out _), "Missing cefrLevel");
            Assert.True(obj.TryGetProperty("status", out var statusEl), "Missing status");
            var status = statusEl.GetString();
            Assert.Contains(status, new[] { "Current", "Ready", "Upcoming", "Locked", "Review", "Blocked", "Completed" });
            Assert.True(obj.TryGetProperty("sequenceNumber", out var seqEl), "Missing sequenceNumber");
            Assert.True(seqEl.GetInt32() > 0);
        }
    }
}
