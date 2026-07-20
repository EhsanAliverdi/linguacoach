using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    // ── Test 5: Phase 20G regression — journey resolves by user ID, not profile ID ──
    //
    // GetJourney previously called ILearningPlanService.GetJourneyAsync(userId, ...),
    // but that method's parameter is a StudentProfileId, not a UserId -- two distinct
    // GUIDs. This meant the endpoint could never find a matching profile and always
    // silently fell back to an empty journey (planStatus "None", totalObjectives 0),
    // even when a real, populated learning plan existed for the student. Found live
    // in production during the Phase 20E/20G pilot walkthrough: the dashboard showed
    // a real plan with objective progress, but /journey showed "complete your
    // placement assessment" as if nothing existed. Fixed via a new
    // GetJourneyForUserAsync method that resolves the profile by UserId first.
    [Fact]
    public async Task GetJourney_ResolvesActivePlan_ByUserIdNotProfileId()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"journey_userid_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Force a real, populated plan to exist for this student — deterministically,
        // without depending on any async/deferred generation triggered by placement.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

            // Adaptive Curriculum Sprint 7 — plan generation now routes against real
            // SkillGraphNode candidates (CurriculumObjective retired), so this test needs at
            // least one Approved/active node at the student's normalized CEFR level (A1, since
            // CreateOnboardedStudentAsync never sets CefrLevel) for the skill
            // BuildObjectiveSequenceAsync's rotation tries first.
            var node = new SkillGraphNode(
                $"a1.speaking.journey_test_{Guid.NewGuid():N}", "Journey Test Node", "Test node description.",
                CefrLevelConstants.A1, CurriculumSkillConstants.Speaking);
            node.Approve(null);
            db.SkillGraphNodes.Add(node);
            await db.SaveChangesAsync();

            var learningPlan = scope.ServiceProvider.GetRequiredService<ILearningPlanService>();
            await learningPlan.GetOrCreatePlanAsync(profile.Id);
        }

        var resp = await client.GetAsync("/api/student/learning-plan/journey");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // The bug returned "None"/0 here even though the plan above was just created.
        Assert.Equal("Active", body.GetProperty("planStatus").GetString());
        Assert.True(body.GetProperty("totalObjectives").GetInt32() > 0,
            "Expected the journey endpoint (resolved via the JWT user ID) to see the " +
            "plan just created for this student's profile ID -- got 0 objectives, " +
            "meaning the user ID -> profile ID resolution is broken again.");
    }
}
