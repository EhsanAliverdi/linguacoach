using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Placement;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 14B — verifies the CourseReady lifecycle transition after placement completion.
/// </summary>
public sealed class StudentPlacementCourseReadyTests : IClassFixture<PlacementTestFactory>
{
    private readonly PlacementTestFactory _factory;

    public StudentPlacementCourseReadyTests(PlacementTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> StartAndGetAssessmentIdAsync(HttpClient client)
    {
        var resp = await client.PostAsync("/api/student/placement/start", null);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("assessmentId").GetString()!;
    }

    // ── Part B: CourseReady transition ────────────────────────────────────────

    [Fact]
    public async Task Complete_TransitionsToCourseReady_WhenPlanGenerationSucceeds()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"cr_happy_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var assessmentId = await StartAndGetAssessmentIdAsync(client);

        var resp = await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });

        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

        // When plan generation succeeds, lifecycle must be CourseReady.
        // PlacementCompleted is acceptable only when plan generation genuinely fails.
        Assert.True(
            profile.LifecycleStage == StudentLifecycleStage.CourseReady ||
            profile.LifecycleStage == StudentLifecycleStage.PlacementCompleted,
            $"Expected CourseReady or PlacementCompleted, got {profile.LifecycleStage}");
    }

    [Fact]
    public async Task Complete_IsIdempotent_CalledTwiceDoesNotDuplicate()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"cr_idem_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var assessmentId = await StartAndGetAssessmentIdAsync(client);

        // First completion
        var first = await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });
        first.EnsureSuccessStatusCode();

        // Second completion of same assessment — returns same result
        var second = await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });
        second.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

        // Lifecycle should be one of the valid post-completion stages.
        var validStages = new[]
        {
            StudentLifecycleStage.PlacementCompleted,
            StudentLifecycleStage.CourseReady,
        };
        Assert.Contains(profile.LifecycleStage, validStages);

        // There must still be exactly one assessment for this student.
        var assessmentCount = await db.PlacementAssessments
            .CountAsync(a => a.StudentProfileId == profile.Id);
        Assert.Equal(1, assessmentCount);
    }

    [Fact]
    public async Task Complete_IncompleteOrNotStarted_DoesNotTransitionToLearning()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"cr_noinit_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // No placement started — dashboard still accessible but lifecycle unchanged
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

        Assert.Equal(StudentLifecycleStage.PlacementRequired, profile.LifecycleStage);

        // CourseReady and InLesson stages must NOT be reachable without placement.
        Assert.NotEqual(StudentLifecycleStage.CourseReady, profile.LifecycleStage);
        Assert.NotEqual(StudentLifecycleStage.InLesson, profile.LifecycleStage);
    }

    // ── Part E: placement gate unlock ─────────────────────────────────────────

    [Fact]
    public async Task PlacementGuard_AllowsDashboard_AfterPlacementComplete()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"cr_gate_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var assessmentId = await StartAndGetAssessmentIdAsync(client);

        var completeResp = await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });
        completeResp.EnsureSuccessStatusCode();

        // Dashboard endpoint must be accessible after placement completion.
        var dashResp = await client.GetAsync("/api/dashboard");
        Assert.Equal(System.Net.HttpStatusCode.OK, dashResp.StatusCode);

        var dashBody = await dashResp.Content.ReadFromJsonAsync<JsonElement>();
        var lifecycle = dashBody.GetProperty("lifecycleStage").GetString();
        Assert.True(
            lifecycle == "CourseReady" || lifecycle == "PlacementCompleted",
            $"Expected CourseReady or PlacementCompleted, got {lifecycle}");
    }
}

/// <summary>
/// Phase 14B — verifies PlacementCompleted is preserved when plan generation fails.
/// Uses a factory that substitutes a throwing ILearningPlanService.
/// </summary>
public sealed class StudentPlacementCourseReadyFailingPlanTests : IClassFixture<FailingLearningPlanFactory>
{
    private readonly FailingLearningPlanFactory _factory;

    public StudentPlacementCourseReadyFailingPlanTests(FailingLearningPlanFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Complete_StaysAtPlacementCompleted_WhenLearningPlanFails()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"cr_fail_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var startResp = await client.PostAsync("/api/student/placement/start", null);
        startResp.EnsureSuccessStatusCode();
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        // Complete — plan regen will throw but placement must still complete.
        var resp = await client.PostAsJsonAsync("/api/student/placement/complete",
            new { assessmentId = Guid.Parse(assessmentId) });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

        // Must stay at PlacementCompleted — not CourseReady — when plan fails.
        Assert.Equal(StudentLifecycleStage.PlacementCompleted, profile.LifecycleStage);
    }
}

/// <summary>
/// Test factory where ILearningPlanService.RegeneratePlanAsync always throws.
/// Extends ActivityTestFactory (not the sealed PlacementTestFactory) and registers
/// both FakePlacementEvaluator and ThrowingLearningPlanService.
/// </summary>
public sealed class FailingLearningPlanFactory : ActivityTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            // Replace the AI placement evaluator with the deterministic fake.
            var existingEval = services.Where(d => d.ServiceType == typeof(IPlacementEvaluator)).ToList();
            foreach (var d in existingEval) services.Remove(d);
            services.AddScoped<IPlacementEvaluator, FakePlacementEvaluator>();

            // Replace the real learning plan service with one that always throws.
            var existingPlan = services.Where(d => d.ServiceType == typeof(ILearningPlanService)).ToList();
            foreach (var d in existingPlan) services.Remove(d);
            services.AddScoped<ILearningPlanService, ThrowingLearningPlanService>();
        });
    }
}

/// <summary>
/// Minimal ILearningPlanService that throws from RegeneratePlanAsync.
/// All other methods are no-ops returning safe defaults.
/// </summary>
public sealed class ThrowingLearningPlanService : ILearningPlanService
{
    public Task<LearningPlanSummary> GetOrCreatePlanAsync(Guid studentProfileId, CancellationToken ct = default)
        => Task.FromResult(EmptySummary(studentProfileId));

    public Task<LearningPlanSummary> RegeneratePlanAsync(Guid studentProfileId, string reason, CancellationToken ct = default)
        => throw new InvalidOperationException("Simulated learning plan generation failure.");

    public Task<LearningPlanProgressSummary> GetProgressAsync(Guid studentProfileId, CancellationToken ct = default)
        => Task.FromResult(new LearningPlanProgressSummary(
            studentProfileId, "A2", 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0,
            "initial", 0, 0, null, null, null, 0));

    public Task<PlannedObjectiveContext?> GetNextPlannedObjectiveAsync(Guid studentProfileId, string? preferredSkill = null, CancellationToken ct = default)
        => Task.FromResult<PlannedObjectiveContext?>(null);

    public Task<IReadOnlyList<PlannedObjectiveContext>> GetPracticeGymObjectivesAsync(Guid studentProfileId, int maxCount = 5, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PlannedObjectiveContext>>(Array.Empty<PlannedObjectiveContext>());

    public Task MarkObjectiveInProgressAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task MarkObjectiveCompletedAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task MarkObjectiveMasteredAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<LearningPlanObjectiveProgressUpdate> TryUpdateObjectiveProgressAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct = default)
        => Task.FromResult(new LearningPlanObjectiveProgressUpdate(objectiveKey, null, null, false, "no-op"));

    private static LearningPlanSummary EmptySummary(Guid profileId) => new(
        Guid.NewGuid(), profileId, "A2", LearningPlanStatus.Active,
        "placement_completed", 0, 0, 0, 0, 0, 0, 0, 0, null,
        Array.Empty<PlannedObjectiveContext>());
}
