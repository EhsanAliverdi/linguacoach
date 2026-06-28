using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for POST /api/activity/{id}/audio-attempt.
/// No STT or AI evaluation — audio is stored and a pending feedback response is returned.
/// </summary>
public sealed class AudioAttemptEndpointTests : IClassFixture<SpeakingRolePlayTestFactory>
{
    private readonly SpeakingRolePlayTestFactory _factory;

    public AudioAttemptEndpointTests(SpeakingRolePlayTestFactory factory) => _factory = factory;

    // ── Auth ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AudioAttempt_Unauthenticated_Returns401()
    {
        var activityId = await CreateActivityAsync(Guid.NewGuid());

        using var form = BuildAudioForm();
        var resp = await _factory.CreateClient().PostAsync($"/api/activity/{activityId}/audio-attempt", form);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Validation ────────────────────────────────────────────────────────

    [Fact]
    public async Task AudioAttempt_MissingFile_Returns400()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"aa_nofile_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = new MultipartFormDataContent();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/audio-attempt", form);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AudioAttempt_InvalidMimeType_Returns400()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"aa_mime_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("not audio"));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(content, "audioFile", "recording.txt");

        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/audio-attempt", form);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("supported", body.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AudioAttempt_UnknownActivity_Returns404()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"aa_404_{Guid.NewGuid():N}@t.com");

        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{Guid.NewGuid()}/audio-attempt", form);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task AudioAttempt_Valid_Returns200WithPendingFeedback()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"aa_ok_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/audio-attempt", form);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("attemptId").GetGuid());
        // score must be null — no AI evaluation
        Assert.Equal(JsonValueKind.Null, body.GetProperty("score").ValueKind);
        // coachSummary must be null — pending state
        Assert.Equal(JsonValueKind.Null, body.GetProperty("coachSummary").ValueKind);
    }

    [Fact]
    public async Task AudioAttempt_Valid_PersistsAttemptWithAudioKey()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"aa_persist_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/audio-attempt", form);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var attempt = db.ActivityAttempts.SingleOrDefault(a => a.Id == attemptId);
        Assert.NotNull(attempt);
        Assert.Equal("[voice recording]", attempt.SubmittedContent);
        Assert.False(string.IsNullOrWhiteSpace(attempt.AudioStorageKey));
        Assert.StartsWith("speaking-recordings/", attempt.AudioStorageKey!);
        Assert.DoesNotContain("tmp", attempt.AudioStorageKey!);
    }

    [Fact]
    public async Task AudioAttempt_Valid_AudioCanBeRetrieved()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"aa_audio_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = BuildAudioForm();
        var submitResp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/audio-attempt", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var body = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        var audioResp = await ClientWithToken(token)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/audio");

        Assert.Equal(HttpStatusCode.OK, audioResp.StatusCode);
        var bytes = await audioResp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task AudioAttempt_WrongOwner_AudioEndpointDenied()
    {
        var (ownerToken, ownerUserId) = await _factory.CreateOnboardedStudentAsync($"aa_own_{Guid.NewGuid():N}@t.com");
        var (otherToken, _) = await _factory.CreateOnboardedStudentAsync($"aa_other_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(ownerUserId);

        using var form = BuildAudioForm();
        var submitResp = await ClientWithToken(ownerToken).PostAsync($"/api/activity/{activityId}/audio-attempt", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var body = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        var audioResp = await ClientWithToken(otherToken)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/audio");

        Assert.Equal(HttpStatusCode.NotFound, audioResp.StatusCode);
    }

    [Fact]
    public async Task AudioAttempt_ResponseDoesNotExposeStorageKey()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"aa_nopath_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);

        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/audio-attempt", form);
        var bodyText = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("speaking-recordings", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StorageKey", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<Guid> CreateActivityAsync(Guid ownerUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "SpeakingRolePlay",
            title = "Audio response test activity",
            scenario = "Describe your typical morning routine.",
            prompt = "Record a 30-second description.",
            maxDurationSeconds = 60,
            interactionMode = "audioResponse",
        });

        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            activityType: ActivityType.SpeakingRolePlay,
            source: ActivitySource.SystemFallback,
            title: "Audio response test activity",
            difficulty: "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: null);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private static MultipartFormDataContent BuildAudioForm(string mimeType = "audio/webm")
    {
        var bytes = new byte[1024];
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
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
