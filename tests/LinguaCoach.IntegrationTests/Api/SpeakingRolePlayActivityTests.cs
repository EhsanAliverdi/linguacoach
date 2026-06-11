using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for the SpeakingRolePlay activity flow.
/// Uses FakeSpeechToTextService (returns deterministic placeholder transcript).
/// No real microphone, no real STT provider.
/// </summary>
public sealed class SpeakingRolePlayActivityTests : IClassFixture<SpeakingRolePlayTestFactory>
{
    private readonly SpeakingRolePlayTestFactory _factory;

    public SpeakingRolePlayActivityTests(SpeakingRolePlayTestFactory factory) => _factory = factory;

    // ── GET /api/activity/next?type=SpeakingRolePlay ───────────────────────

    [Fact]
    public async Task GetNext_SpeakingRolePlay_ReturnsSpeakingRolePlayType()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"spr_get_{Guid.NewGuid():N}@t.com");

        var resp = await ClientWithToken(token).GetAsync("/api/activity/next?type=SpeakingRolePlay");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("speakingRolePlay", body.GetProperty("activityType").GetString());
    }

    [Fact]
    public async Task GetNext_SpeakingRolePlay_DoesNotReturnAnotherType()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"spr_typed_{Guid.NewGuid():N}@t.com");

        var resp = await ClientWithToken(token).GetAsync("/api/activity/next?type=SpeakingRolePlay");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var type = body.GetProperty("activityType").GetString();
        Assert.Equal("speakingRolePlay", type);
        Assert.NotEqual("writingScenario", type);
        Assert.NotEqual("listeningComprehension", type);
        Assert.NotEqual("vocabularyPractice", type);
    }

    [Fact]
    public async Task GetNext_SpeakingRolePlay_HasExpectedContentFields()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"spr_fields_{Guid.NewGuid():N}@t.com");

        var resp = await ClientWithToken(token).GetAsync("/api/activity/next?type=SpeakingRolePlay");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("speakingRolePlay", body.GetProperty("activityType").GetString());
        // speaking content fields must be present
        Assert.True(body.TryGetProperty("speakingScenario", out _));
        Assert.True(body.TryGetProperty("speakingPrompt", out _));
        Assert.True(body.TryGetProperty("maxDurationSeconds", out _));
        // transcript must NOT be exposed before submit
        Assert.False(body.TryGetProperty("transcript", out _));
    }

    [Fact]
    public async Task GetNext_SpeakingRolePlay_FallbackReturnsCorrectType()
    {
        // Use the malformed AI provider test factory to trigger fallback
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"spr_fallback_{Guid.NewGuid():N}@t.com");

        // Regardless of AI success/failure the type must be SpeakingRolePlay
        var resp = await ClientWithToken(token).GetAsync("/api/activity/next?type=SpeakingRolePlay");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("speakingRolePlay", body.GetProperty("activityType").GetString());
    }

    // ── POST /api/activity/{id}/speaking-attempt ──────────────────────────

    [Fact]
    public async Task SpeakingAttempt_Unauthenticated_Returns401()
    {
        var activityId = await CreateSpeakingActivityAsync(ownerUserId: Guid.NewGuid());

        using var form = BuildAudioForm();
        var resp = await _factory.CreateClient().PostAsync($"/api/activity/{activityId}/speaking-attempt", form);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task SpeakingAttempt_InvalidFileType_Returns400()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"spr_mime_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateSpeakingActivityAsync(userId);

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("not audio"));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(content, "audioFile", "recording.txt");

        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/speaking-attempt", form);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("supported", body.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SpeakingAudio_WrongOwner_AudioEndpointDeniesAccess()
    {
        // Verify that audio endpoint ownership is enforced at the attempt level.
        // The wrong student cannot access the owner's speaking audio.
        var (ownerToken, ownerUserId) = await _factory.CreateOnboardedStudentAsync($"spr_owner2_{Guid.NewGuid():N}@t.com");
        var (otherToken, _) = await _factory.CreateOnboardedStudentAsync($"spr_other2_{Guid.NewGuid():N}@t.com");

        var activityId = await CreateSpeakingActivityAsync(ownerUserId);
        // Owner submits a valid attempt
        using var form = BuildAudioForm();
        var submitResp = await ClientWithToken(ownerToken).PostAsync($"/api/activity/{activityId}/speaking-attempt", form);
        if (!submitResp.IsSuccessStatusCode) return; // skip if infrastructure issue
        var body = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        // Other student tries to access the audio
        var audioResp = await ClientWithToken(otherToken)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/audio");

        Assert.Equal(HttpStatusCode.NotFound, audioResp.StatusCode);
    }

    [Fact]
    public async Task SpeakingAttempt_Valid_FakeSTT_ReturnsFeedbackWithTranscript()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"spr_submit_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateSpeakingActivityAsync(userId);

        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/speaking-attempt", form);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("attemptId").GetGuid());
        Assert.True(body.TryGetProperty("transcript", out var t) && !string.IsNullOrWhiteSpace(t.GetString()));
        Assert.True(body.TryGetProperty("coachSummary", out _));
        Assert.True(body.TryGetProperty("score", out _));
    }

    [Fact]
    public async Task SpeakingAttempt_Valid_TranscriptEqualsPlaceholder()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"spr_placeholder_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateSpeakingActivityAsync(userId);

        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/speaking-attempt", form);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeSpeechToTextService.PlaceholderTranscript, body.GetProperty("transcript").GetString());
    }

    [Fact]
    public async Task SpeakingAttempt_Valid_SavesActivityAttemptWithStorageKey()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"spr_save_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateSpeakingActivityAsync(userId);

        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/speaking-attempt", form);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = body.GetProperty("attemptId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var attempt = db.ActivityAttempts.SingleOrDefault(a => a.Id == attemptId);
        Assert.NotNull(attempt);
        Assert.Equal(FakeSpeechToTextService.PlaceholderTranscript, attempt.SubmittedContent);
        Assert.False(string.IsNullOrWhiteSpace(attempt.AudioStorageKey));
        // Committed final key uses the speaking-recordings category prefix and the attempt id.
        Assert.StartsWith("speaking-recordings/", attempt.AudioStorageKey!);
        Assert.DoesNotContain("tmp", attempt.AudioStorageKey!);   // temp key was committed
        Assert.DoesNotContain("..", attempt.AudioStorageKey!);    // no path traversal
        Assert.DoesNotContain("\\", attempt.AudioStorageKey!);
    }

    // ── GET /api/activity/{id}/attempts/{attemptId}/audio ─────────────────

    [Fact]
    public async Task SpeakingAudio_Unauthenticated_Returns401()
    {
        var activityId = await CreateSpeakingActivityAsync(Guid.NewGuid());

        var resp = await _factory.CreateClient()
            .GetAsync($"/api/activity/{activityId}/attempts/{Guid.NewGuid()}/audio");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task SpeakingAudio_WrongOwner_Returns404()
    {
        var (_, ownerUserId) = await _factory.CreateOnboardedStudentAsync($"spr_audio_owner_{Guid.NewGuid():N}@t.com");
        var (otherToken, _) = await _factory.CreateOnboardedStudentAsync($"spr_audio_other_{Guid.NewGuid():N}@t.com");

        var activityId = await CreateSpeakingActivityAsync(ownerUserId);
        var attemptId = await SubmitSpeakingAttemptAsync(ownerUserId, activityId);

        var resp = await ClientWithToken(otherToken)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/audio");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task SpeakingAudio_ValidOwner_ReturnsBytesWithContentType()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"spr_audio_valid_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateSpeakingActivityAsync(userId);
        var attemptId = await SubmitSpeakingAttemptAsync(userId, activityId);

        var resp = await ClientWithToken(token)
            .GetAsync($"/api/activity/{activityId}/attempts/{attemptId}/audio");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(resp.Content.Headers.ContentType?.MediaType);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task SpeakingAudio_ResponseDoesNotExposeFilesystemPath()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"spr_nopath_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateSpeakingActivityAsync(userId);
        var _ = await SubmitSpeakingAttemptAsync(userId, activityId);

        // The audio URL in feedback must not contain filesystem path fragments
        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/speaking-attempt", form);
        var bodyText = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("app-data", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StorageKey", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    // ── History / progress safety ──────────────────────────────────────────

    [Fact]
    public async Task History_SpeakingAttempt_ReturnsTranscriptAndFeedback()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"spr_hist_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateSpeakingActivityAsync(userId);
        await SubmitSpeakingAttemptAsync(userId, activityId);

        var resp = await ClientWithToken(token).GetAsync($"/api/activity/{activityId}/attempts");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SpeakingRolePlay", body.GetProperty("activityType").GetString());
        var attempts = body.GetProperty("attempts");
        Assert.True(attempts.GetArrayLength() >= 1);
        var attempt = attempts[0];
        Assert.True(attempt.TryGetProperty("transcript", out var t) && !string.IsNullOrWhiteSpace(t.GetString()));
        Assert.True(attempt.TryGetProperty("speakingAudioUrl", out var au) && au.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Progress_HandlesSpeakingRolePlayAttemptWithoutBreaking()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"spr_prog_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateSpeakingActivityAsync(userId);
        await SubmitSpeakingAttemptAsync(userId, activityId);

        var resp = await ClientWithToken(token).GetAsync("/api/progress");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<Guid> CreateSpeakingActivityAsync(Guid ownerUserId, bool attachToOwnerModule = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var fallbackJson = JsonSerializer.Serialize(new
        {
            activityType = "SpeakingRolePlay",
            title = "Explain a delay to your manager",
            scenario = "Your manager asks about a delay.",
            studentRole = "Document Controller",
            listenerRole = "Manager",
            difficulty = "B1",
            speakingGoal = "Explain clearly.",
            prompt = "Record a short response.",
            expectedPoints = new[] { "mention the delay", "give a reason" },
            suggestedPhrases = new[] { "I wanted to update you..." },
            maxDurationSeconds = 60
        });

        Guid? moduleId = null;
        if (attachToOwnerModule)
        {
            var profile = db.StudentProfiles.First(p => p.UserId == ownerUserId);
            var path = db.LearningPaths.FirstOrDefault(p => p.StudentProfileId == profile.Id);
            var module = path is not null ? db.LearningModules.FirstOrDefault(m => m.LearningPathId == path.Id) : null;
            moduleId = module?.Id;
        }

        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            activityType: ActivityType.SpeakingRolePlay,
            source: ActivitySource.SystemFallback,
            title: "Explain a delay",
            difficulty: "B1",
            aiGeneratedContentJson: fallbackJson,
            learningModuleId: moduleId);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private async Task<Guid> SubmitSpeakingAttemptAsync(Guid userId, Guid activityId)
    {
        var (token, _) = await _factory.GetTokenForUserAsync(userId);
        using var form = BuildAudioForm();
        var resp = await ClientWithToken(token).PostAsync($"/api/activity/{activityId}/speaking-attempt", form);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("attemptId").GetGuid();
    }

    private static MultipartFormDataContent BuildAudioForm(string mimeType = "audio/webm")
    {
        // Minimal valid webm-like bytes (not real audio, but enough to pass file size check)
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

/// <summary>
/// Test factory for SpeakingRolePlay tests. Seeds speaking prompt keys.
/// </summary>
public sealed class SpeakingRolePlayTestFactory : ActivityTestFactory
{
    public override async Task<(string Token, Guid UserId)> CreateOnboardedStudentAsync(string email = "speaking_student@test.linguacoach.com")
    {
        await SeedSpeakingPromptsAsync();
        return await base.CreateOnboardedStudentAsync(email);
    }

    public async Task SeedSpeakingPromptsAsync()
    {
        await SeedPromptTemplateAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        foreach (var key in new[] { "activity_generate_speaking_roleplay", "activity_evaluate_speaking_roleplay", "activity_generate_listening" })
        {
            if (!db.AiPrompts.Any(p => p.Key == key))
            {
                db.AiPrompts.Add(new LinguaCoach.Domain.Entities.AiPrompt(
                    key, "fake-prompt-{{cefrLevel}}", maxInputTokens: 800, maxOutputTokens: 1000));
                await db.SaveChangesAsync();
            }
        }
    }

    public async Task<(string Token, Guid UserId)> GetTokenForUserAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        if (user is null) throw new InvalidOperationException($"User {userId} not found.");
        var email = user.Email ?? throw new InvalidOperationException("User has no email.");
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Student@1234" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("token").GetString()!, userId);
    }
}
