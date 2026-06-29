using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for GET /api/activity/{id}/attempts/{attemptId}/evaluation.
/// Phase 16F — speaking evaluation foundation.
/// </summary>
public sealed class SpeakingEvaluationEndpointTests : IClassFixture<SpeakingRolePlayTestFactory>
{
    private readonly SpeakingRolePlayTestFactory _factory;

    public SpeakingEvaluationEndpointTests(SpeakingRolePlayTestFactory factory) => _factory = factory;

    // ── Auth ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvaluation_Unauthenticated_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync($"/api/activity/{Guid.NewGuid()}/attempts/{Guid.NewGuid()}/evaluation");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Not found paths ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvaluation_NoEvaluationRecord_Returns404()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"eval_404_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = BuildAudioForm();
        var submitResp = await ClientWithToken(token)
            .PostAsync($"/api/activity/{activityId}/audio-attempt", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var body = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        // NoOp provider: evaluation is created but marked NotSupported immediately — may or may not be there.
        // Test by fetching a completely random attemptId that has no record.
        var evalResp = await ClientWithToken(token)
            .GetAsync($"/api/activity/{activityId}/attempts/{Guid.NewGuid()}/evaluation");

        Assert.Equal(HttpStatusCode.NotFound, evalResp.StatusCode);
    }

    // ── Ownership ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvaluation_WrongOwner_Returns404()
    {
        var (ownerToken, ownerUserId) = await _factory.CreateOnboardedStudentAsync($"eval_own_{Guid.NewGuid():N}@t.com");
        var (otherToken, _) = await _factory.CreateOnboardedStudentAsync($"eval_oth_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(ownerUserId);

        using var form = BuildAudioForm();
        var submitResp = await ClientWithToken(ownerToken)
            .PostAsync($"/api/activity/{activityId}/audio-attempt", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var body = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        // Manually insert an evaluation record so there is something to protect.
        await InsertEvaluationAsync(attemptId, ownerUserId, activityId);

        var evalResp = await ClientWithToken(otherToken)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/evaluation");

        Assert.Equal(HttpStatusCode.NotFound, evalResp.StatusCode);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvaluation_WhenRecordExists_ReturnsEvaluationDto()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"eval_ok_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = BuildAudioForm();
        var submitResp = await ClientWithToken(token)
            .PostAsync($"/api/activity/{activityId}/audio-attempt", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var body = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        await InsertEvaluationAsync(attemptId, userId, activityId);

        var evalResp = await ClientWithToken(token)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/evaluation");

        Assert.Equal(HttpStatusCode.OK, evalResp.StatusCode);
        var evalBody = await evalResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(attemptId.ToString(), evalBody.GetProperty("attemptId").GetString());
        Assert.True(evalBody.TryGetProperty("status", out var statusProp));
        Assert.False(string.IsNullOrEmpty(statusProp.GetString()));
    }

    [Fact]
    public async Task GetEvaluation_ResponseDoesNotExposeStorageKey()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"eval_nokey_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = BuildAudioForm();
        var submitResp = await ClientWithToken(token)
            .PostAsync($"/api/activity/{activityId}/audio-attempt", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var body = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        await InsertEvaluationAsync(attemptId, userId, activityId);

        var evalResp = await ClientWithToken(token)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/evaluation");
        var raw = await evalResp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("speaking-recordings/", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StorageKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audioStorageKey", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── Audio submission creates pending eval (NoOp resolves immediately) ─────

    [Fact]
    public async Task AudioAttemptSubmission_DoesNotBlockOnEvaluationError()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"eval_nonblock_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token)
            .PostAsync($"/api/activity/{activityId}/audio-attempt", form);

        // Must succeed regardless of evaluation service state (NoOp or any error).
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task InsertEvaluationAsync(Guid attemptId, Guid userId, Guid activityId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileId = await db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstAsync();

        var eval = SpeakingEvaluation.CreatePending(attemptId, profileId, activityId);
        eval.MarkNotSupported();
        db.SpeakingEvaluations.Add(eval);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateActivityAsync(Guid ownerUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var contentJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            activityType = "SpeakingRolePlay",
            title = "Evaluation endpoint test activity",
            scenario = "Describe your typical morning routine.",
            prompt = "Record a 30-second description.",
            maxDurationSeconds = 60,
            interactionMode = "audioResponse",
        });

        var activity = new LearningActivity(
            activityType: ActivityType.SpeakingRolePlay,
            source: ActivitySource.SystemFallback,
            title: "Evaluation endpoint test activity",
            difficulty: "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: null);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private static MultipartFormDataContent BuildAudioForm()
    {
        var bytes = new byte[1024];
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        var form = new MultipartFormDataContent();
        form.Add(content, "audioFile", "recording.webm");
        return form;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
