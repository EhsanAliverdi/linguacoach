using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Infrastructure.Placement;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 15B — consolidated student dashboard summary endpoint.
/// Uses PlacementTestFactory (deterministic scoring, fake AI, no live calls).
/// </summary>
public sealed class StudentDashboardSummaryTests : IClassFixture<PlacementTestFactory>
{
    private readonly PlacementTestFactory _factory;

    public StudentDashboardSummaryTests(PlacementTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(string Token, Guid UserId)> CreateCourseReadyStudentAsync(string emailSuffix)
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"ds15b_{emailSuffix}_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var startResp = await client.PostAsync("/api/student/placement/start", null);
        startResp.EnsureSuccessStatusCode();
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        var completeResp = await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });
        completeResp.EnsureSuccessStatusCode();

        return (token, userId);
    }

    // ── Test 1: Summary returns 200 for an onboarded student ─────────────────

    [Fact]
    public async Task Summary_Returns200_ForPlacementRequiredStudent()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"ds15b_pr_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("profile", out _));
        Assert.True(body.TryGetProperty("courseReadiness", out _));
        Assert.True(body.TryGetProperty("todaySession", out _));
        Assert.True(body.TryGetProperty("quickStats", out _));
        Assert.True(body.TryGetProperty("warnings", out _));
    }

    // ── Test 2: TodaySession returns NotAvailable for PlacementRequired ──────

    [Fact]
    public async Task Summary_TodaySession_IsNotAvailable_ForPlacementRequiredStudent()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"ds15b_nots_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/dashboard/summary");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var status = body.GetProperty("todaySession").GetProperty("status").GetString();
        Assert.Equal("NotAvailable", status);
    }

    // ── Test 3: Practice returns Preparing when pool is empty ─────────────────

    [Fact]
    public async Task Summary_Practice_IsPreparingOrNotAvailable_WhenPoolEmpty()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"ds15b_prac_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/dashboard/summary");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var practiceStatus = body.GetProperty("practice").GetProperty("status").GetString();
        Assert.True(
            practiceStatus is "Preparing" or "NotAvailable",
            $"Expected Preparing or NotAvailable, got {practiceStatus}");
    }

    // ── Test 4: Placement incomplete state is surfaced ────────────────────────

    [Fact]
    public async Task Summary_CourseReadiness_PlacementRequired_WhenNotPlaced()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"ds15b_plreq_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/dashboard/summary");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var cr = body.GetProperty("courseReadiness");
        Assert.True(cr.GetProperty("placementRequired").GetBoolean());
        Assert.False(cr.GetProperty("isLearningReady").GetBoolean());

        var warnings = body.GetProperty("warnings");
        Assert.True(warnings.GetProperty("placementIncomplete").GetBoolean());
    }

    // ── Test 5: No cross-student data leak ────────────────────────────────────

    [Fact]
    public async Task Summary_DoesNotLeak_AnotherStudentsData()
    {
        var (tokenA, _) = await _factory.CreateOnboardedStudentAsync(
            $"ds15b_sec_a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateOnboardedStudentAsync(
            $"ds15b_sec_b_{Guid.NewGuid():N}@test.com");

        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var respA = await clientA.GetAsync("/api/student/dashboard/summary");
        var respB = await clientB.GetAsync("/api/student/dashboard/summary");

        respA.EnsureSuccessStatusCode();
        respB.EnsureSuccessStatusCode();

        var bodyA = await respA.Content.ReadFromJsonAsync<JsonElement>();
        var bodyB = await respB.Content.ReadFromJsonAsync<JsonElement>();

        var nameA = bodyA.GetProperty("profile").GetProperty("displayName").GetString();
        var nameB = bodyB.GetProperty("profile").GetProperty("displayName").GetString();

        Assert.NotEqual(nameA, nameB);
    }

    // ── Test 7: LearningPlan section is present after placement ─────────────

    [Fact]
    public async Task Summary_LearningPlan_SectionPresent_AfterPlacement()
    {
        var (token, _) = await CreateCourseReadyStudentAsync("lp");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/dashboard/summary");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Section fields always present regardless of plan generation success.
        Assert.True(body.TryGetProperty("learningPlan", out var lp));
        Assert.True(lp.TryGetProperty("totalObjectives", out _));
        Assert.True(lp.TryGetProperty("modulesCompleted", out _));
        Assert.True(lp.TryGetProperty("progressPercent", out _));

        var status = body.GetProperty("courseReadiness")
            .GetProperty("lifecycleStatus").GetString();
        Assert.True(
            status is "CourseReady" or "PlacementCompleted",
            $"Expected CourseReady or PlacementCompleted, got {status}");
    }

    // ── Test 8: QuickStats maps CEFR, streak, activities, review queue ───────

    [Fact]
    public async Task Summary_QuickStats_ArePresent()
    {
        var (token, _) = await CreateCourseReadyStudentAsync("qs");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/student/dashboard/summary");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var qs = body.GetProperty("quickStats");

        // Fields must be present (values depend on seed data).
        Assert.True(qs.TryGetProperty("currentCefr", out _));
        Assert.True(qs.TryGetProperty("streakDays", out _));
        Assert.True(qs.TryGetProperty("activitiesCompleted", out _));
        Assert.True(qs.TryGetProperty("reviewQueueCount", out _));
    }

    // ── Test 9: Unauthenticated request returns 401 ──────────────────────────

    [Fact]
    public async Task Summary_Returns401_WhenUnauthenticated()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/student/dashboard/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}

/// <summary>
/// Factory that adds fake placement + a throwing practice stub.
/// Derives from ActivityTestFactory (PlacementTestFactory is sealed).
/// Must be used as IClassFixture so IAsyncLifetime.InitializeAsync runs.
/// </summary>
public sealed class ThrowingPracticeTestFactory : ActivityTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            // Throwing practice service.
            var existingPractice = services.Where(d => d.ServiceType == typeof(IPracticeGymSuggestionService)).ToList();
            foreach (var d in existingPractice) services.Remove(d);
            services.AddScoped<IPracticeGymSuggestionService, ThrowingPracticeGymSuggestionService>();
        });
    }
}

/// <summary>
/// Separate test class so ThrowingPracticeTestFactory runs as IClassFixture
/// (required for IAsyncLifetime.InitializeAsync to open the SQLite connection).
/// </summary>
public sealed class StudentDashboardSummaryIsolationTests
    : IClassFixture<ThrowingPracticeTestFactory>
{
    private readonly ThrowingPracticeTestFactory _factory;

    public StudentDashboardSummaryIsolationTests(ThrowingPracticeTestFactory factory)
    {
        _factory = factory;
    }

    // ── Test 6: Optional section failure does not return 500 ─────────────────

    [Fact]
    public async Task Summary_Returns200_WhenPracticeSectionThrows()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"ds15b_throw_{Guid.NewGuid():N}@test.com");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/student/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var practiceStatus = body.GetProperty("practice").GetProperty("status").GetString();
        Assert.Equal("NotAvailable", practiceStatus);

        var warnings = body.GetProperty("warnings");
        Assert.True(warnings.GetProperty("practiceUnavailable").GetBoolean());
    }
}

internal sealed class ThrowingPracticeGymSuggestionService : IPracticeGymSuggestionService
{
    public Task<PracticeGymSuggestionsDto> GetSuggestionsForStudentAsync(
        Guid studentId, CancellationToken ct = default)
        => throw new InvalidOperationException("Simulated practice gym failure.");

    public Task<StartSuggestionResult> StartSuggestionAsync(
        Guid studentId, Guid readinessItemId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task TryMarkConsumedAsync(
        Guid studentId, Guid readinessItemId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
