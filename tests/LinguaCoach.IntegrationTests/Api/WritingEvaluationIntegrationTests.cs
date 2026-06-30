using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Activity factory with WritingEvaluation enabled (NoOp provider).</summary>
public sealed class WritingEvaluationEnabledTestFactory : ActivityTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("WritingEvaluation:Enabled", "true");
        builder.UseSetting("WritingEvaluation:Provider", "NoOp");
    }
}

/// <summary>
/// Integration tests for Phase 17A writing evaluation foundation.
/// Covers submission trigger, student GET endpoint, ownership, admin listing, and the NoOp job path.
/// </summary>
public sealed class WritingEvaluationIntegrationTests
    : IClassFixture<WritingEvaluationEnabledTestFactory>, IClassFixture<ActivityTestFactory>
{
    private readonly WritingEvaluationEnabledTestFactory _enabled;
    private readonly ActivityTestFactory _disabled;

    public WritingEvaluationIntegrationTests(
        WritingEvaluationEnabledTestFactory enabled, ActivityTestFactory disabled)
    {
        _enabled = enabled;
        _disabled = disabled;
    }

    // ── Submission trigger ─────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitWritingAttempt_WhenEnabled_CreatesPendingEvaluation()
    {
        var (token, userId) = await _enabled.CreateOnboardedStudentAsync($"we_pending_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateWritingActivityAsync(_enabled);

        var attemptId = await SubmitWritingAsync(_enabled, token, activityId);

        using var scope = _enabled.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eval = await db.WritingEvaluations.FirstOrDefaultAsync(e => e.ActivityAttemptId == attemptId);
        Assert.NotNull(eval);
        Assert.Equal(WritingEvaluationStatus.Pending, eval!.Status);
    }

    [Fact]
    public async Task SubmitWritingAttempt_WhenDisabled_CreatesNoEvaluation()
    {
        var (token, userId) = await _disabled.CreateOnboardedStudentAsync($"we_disabled_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateWritingActivityAsync(_disabled);

        var attemptId = await SubmitWritingAsync(_disabled, token, activityId);

        using var scope = _disabled.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var exists = await db.WritingEvaluations.AnyAsync(e => e.ActivityAttemptId == attemptId);
        Assert.False(exists);
    }

    [Fact]
    public async Task SubmitNonWritingActivity_DoesNotCreateEvaluation()
    {
        var (token, userId) = await _enabled.CreateOnboardedStudentAsync($"we_nonwriting_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateReadingActivityAsync(_enabled);

        // ReadingTask flows the legacy AI path with text but is not a WritingScenario.
        var resp = await ClientWithToken(_enabled, token).PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "My summary of the reading passage." });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        using var scope = _enabled.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var exists = await db.WritingEvaluations.AnyAsync(e => e.ActivityAttemptId == attemptId);
        Assert.False(exists);
    }

    [Fact]
    public async Task SubmitWritingAttempt_Twice_DoesNotDuplicateEvaluation()
    {
        var (token, userId) = await _enabled.CreateOnboardedStudentAsync($"we_dupe_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateWritingActivityAsync(_enabled);

        var attemptId = await SubmitWritingAsync(_enabled, token, activityId);

        // Re-request evaluation for the same attempt directly via the service.
        using var scope = _enabled.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IWritingEvaluationService>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileId = await db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).FirstAsync();
        await svc.RequestEvaluationAsync(attemptId, profileId, activityId);

        var count = await db.WritingEvaluations.CountAsync(e => e.ActivityAttemptId == attemptId);
        Assert.Equal(1, count);
    }

    // ── NoOp job path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NoOpProvider_AfterJobRun_MarksNotSupported()
    {
        var (token, userId) = await _enabled.CreateOnboardedStudentAsync($"we_noop_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateWritingActivityAsync(_enabled);
        var attemptId = await SubmitWritingAsync(_enabled, token, activityId);

        using var scope = _enabled.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IWritingEvaluationService>();
        await svc.ProcessPendingAsync(maxBatch: 50);

        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eval = await db.WritingEvaluations.FirstAsync(e => e.ActivityAttemptId == attemptId);
        Assert.Equal(WritingEvaluationStatus.NotSupported, eval.Status);
    }

    // ── Student GET endpoint ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetWritingEvaluation_Unauthenticated_Returns401()
    {
        var resp = await _enabled.CreateClient()
            .GetAsync($"/api/activity/{Guid.NewGuid()}/attempts/{Guid.NewGuid()}/writing-evaluation");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetWritingEvaluation_Owner_ReturnsDto()
    {
        var (token, userId) = await _enabled.CreateOnboardedStudentAsync($"we_get_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateWritingActivityAsync(_enabled);
        var attemptId = await SubmitWritingAsync(_enabled, token, activityId);

        var resp = await ClientWithToken(_enabled, token)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/writing-evaluation");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(attemptId.ToString(), body.GetProperty("attemptId").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task GetWritingEvaluation_WrongOwner_Returns404()
    {
        var (ownerToken, _) = await _enabled.CreateOnboardedStudentAsync($"we_own_{Guid.NewGuid():N}@t.com");
        var (otherToken, _) = await _enabled.CreateOnboardedStudentAsync($"we_oth_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateWritingActivityAsync(_enabled);
        var attemptId = await SubmitWritingAsync(_enabled, ownerToken, activityId);

        var resp = await ClientWithToken(_enabled, otherToken)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/writing-evaluation");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Admin endpoint ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_CanListStudentWritingEvaluations()
    {
        var (token, userId) = await _enabled.CreateOnboardedStudentAsync($"we_admin_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateWritingActivityAsync(_enabled);
        var attemptId = await SubmitWritingAsync(_enabled, token, activityId);

        Guid profileId;
        using (var scope = _enabled.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            profileId = await db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).FirstAsync();
        }

        var adminToken = await _enabled.CreateAdminAndGetTokenAsync();
        var resp = await ClientWithToken(_enabled, adminToken)
            .GetAsync($"/api/admin/students/{profileId}/writing-evaluations");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(items.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Admin_Endpoint_RequiresAdminRole()
    {
        var (token, _) = await _enabled.CreateOnboardedStudentAsync($"we_nonadmin_{Guid.NewGuid():N}@t.com");
        var resp = await ClientWithToken(_enabled, token)
            .GetAsync($"/api/admin/students/{Guid.NewGuid()}/writing-evaluations");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<Guid> SubmitWritingAsync(ActivityTestFactory factory, string token, Guid activityId)
    {
        var resp = await ClientWithToken(factory, token).PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Mr Ahmadi, I wanted to follow up on the pending approval. Best regards, Sara." });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("attemptId").GetGuid();
    }

    private static async Task<Guid> CreateWritingActivityAsync(ActivityTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "WritingScenario",
            title = "Follow up on a pending approval",
            scenario = "You submitted a document five days ago.",
            prompt = "Write a short professional follow-up email.",
        });

        var activity = new LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.SystemFallback,
            title: "Writing evaluation test activity",
            difficulty: "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: null);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private static async Task<Guid> CreateReadingActivityAsync(ActivityTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "ReadingTask",
            title = "Read and summarise",
            passage = "A short workplace passage.",
            prompt = "Summarise the passage in two sentences.",
        });

        var activity = new LearningActivity(
            activityType: ActivityType.ReadingTask,
            source: ActivitySource.SystemFallback,
            title: "Reading test activity",
            difficulty: "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: null);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private static HttpClient ClientWithToken(ActivityTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
