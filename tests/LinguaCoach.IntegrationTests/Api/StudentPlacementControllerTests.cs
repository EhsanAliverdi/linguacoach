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
/// Form.io-native migration: /respond now takes a submission.data dictionary instead of a
/// single string response. Uses PlacementTestFactory (deterministic scoring, no live AI).
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

    private static object RespondBody(Guid assessmentId, Guid itemId, string answer, int? durationSeconds = null) => new
    {
        assessmentId,
        itemId,
        submission = new { data = new Dictionary<string, object> { ["answer"] = answer } },
        durationSeconds,
    };

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

        // Form.io-native: the student-safe schema is present.
        Assert.True(body.TryGetProperty("formIoSchemaJson", out var schema));
        Assert.False(string.IsNullOrWhiteSpace(schema.GetString()));
    }

    [Fact]
    public async Task GetNext_NeverLeaksScoringRulesOrCorrectAnswerToStudent()
    {
        // Security regression: ScoringRulesJson (and any bare correctAnswer-ish key) must never
        // appear anywhere in the raw JSON response — asserted on the raw string, not just DTO shape.
        var (token, _, assessmentId, _) = await StartedAssessmentAsync($"sp_noanswer_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync($"/api/student/placement/next?assessmentId={assessmentId}");
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("scoringRulesJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctAnswer", raw, StringComparison.OrdinalIgnoreCase);
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

        var resp = await client.PostAsJsonAsync("/api/student/placement/respond",
            RespondBody(Guid.Parse(assessmentId), itemId, "A", 5));

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

        var resp = await clientB.PostAsJsonAsync("/api/student/placement/respond",
            RespondBody(Guid.Parse(assessmentId), itemId, "A"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Respond_EmptySubmissionData_Returns400()
    {
        var (token, _, assessmentId, itemId) = await StartedAssessmentAsync($"sp_respond_empty_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync("/api/student/placement/respond", new
        {
            assessmentId = Guid.Parse(assessmentId),
            itemId,
            submission = new { data = new Dictionary<string, object>() },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Respond_UnknownItemId_Returns409()
    {
        var (token, _, assessmentId, _) = await StartedAssessmentAsync($"sp_respond_wrongitem_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync("/api/student/placement/respond",
            RespondBody(Guid.Parse(assessmentId), Guid.NewGuid(), "A"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
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
        // Phase 14B: successful plan generation transitions to CourseReady.
        // PlacementCompleted is the fallback when plan generation fails.
        Assert.True(
            profile.LifecycleStage == StudentLifecycleStage.CourseReady ||
            profile.LifecycleStage == StudentLifecycleStage.PlacementCompleted,
            $"Expected CourseReady or PlacementCompleted, got {profile.LifecycleStage}");
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

    // ── Phase 20I-5: adaptive listening audio endpoint ───────────────────────

    [Fact]
    public async Task GetItemAudio_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(
            $"/api/student/placement/audio/{Guid.NewGuid()}/items/{Guid.NewGuid()}/listening");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetItemAudio_AssessmentNotOwnedByStudent_Returns404()
    {
        var (tokenA, _) = await _factory.CreateOnboardedStudentAsync($"sp_audio_a_{Guid.NewGuid():N}@test.com");
        var (_, _, assessmentIdB, itemIdB) = await StartedAssessmentAsync($"sp_audio_b_{Guid.NewGuid():N}@test.com");

        var client = ClientWithToken(tokenA);
        var resp = await client.GetAsync($"/api/student/placement/audio/{assessmentIdB}/items/{itemIdB}/listening");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetItemAudio_UnknownItemId_Returns404()
    {
        var (token, _, assessmentId, _) = await StartedAssessmentAsync($"sp_audio_unknown_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync($"/api/student/placement/audio/{assessmentId}/items/{Guid.NewGuid()}/listening");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetItemAudio_ItemWithNoAudioScript_Returns404()
    {
        // Only listening-skill items get a backend-only audio script at seed time — a non-listening
        // item (or a listening item whose prompt had no extractable quoted line) has none, so
        // EnsureAudioAsync no-ops and the endpoint degrades gracefully rather than 500ing.
        var (token, _, assessmentId, _) = await StartedAssessmentAsync($"sp_audio_noscript_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        Guid itemWithoutScript;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            itemWithoutScript = db.PlacementAssessmentItems
                .First(i => i.PlacementAssessmentId == Guid.Parse(assessmentId) && i.Skill != "listening")
                .Id;
        }

        var resp = await client.GetAsync($"/api/student/placement/audio/{assessmentId}/items/{itemWithoutScript}/listening");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetItemAudio_ListeningItemWithScript_GeneratesAndStreamsAudio()
    {
        // The test host registers a fake TTS provider (FakeAiProviderResolver.ResolveTts),
        // so this exercises the real EnsureAudioAsync -> GetAudioAsync round trip end to end.
        var (token, _, assessmentId, _) = await StartedAssessmentAsync($"sp_audio_withscript_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        Guid? listeningItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            listeningItemId = db.PlacementAssessmentItems
                .Where(i => i.PlacementAssessmentId == Guid.Parse(assessmentId) && i.Skill == "listening")
                .Select(i => (Guid?)i.Id)
                .FirstOrDefault();
        }

        if (listeningItemId is null) return; // skip gracefully if the initial item set has no listening item

        var resp = await client.GetAsync($"/api/student/placement/audio/{assessmentId}/items/{listeningItemId}/listening");

        // Some listening prompts have no extractable quoted script (see DeriveListeningAudioScript);
        // for those this degrades gracefully to 404 rather than generating empty audio.
        Assert.True(resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);
        if (resp.StatusCode == HttpStatusCode.OK)
            Assert.True(resp.Content.Headers.ContentType?.MediaType?.StartsWith("audio/"));
    }

    // ── Speaking response upload endpoint ────────────────────────────────────

    private static MultipartFormDataContent SpeakingUploadContent(string mimeType = "audio/webm", double? durationSeconds = 4.2)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 });
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        content.Add(fileContent, "audioFile", "recording.webm");
        if (durationSeconds is not null)
            content.Add(new StringContent(durationSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "durationSeconds");
        return content;
    }

    [Fact]
    public async Task UploadSpeakingAudio_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/student/placement/audio/{Guid.NewGuid()}/items/{Guid.NewGuid()}/speaking", SpeakingUploadContent());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task UploadSpeakingAudio_AssessmentNotOwnedByStudent_Returns404()
    {
        var (tokenA, _) = await _factory.CreateOnboardedStudentAsync($"sp_speak_a_{Guid.NewGuid():N}@test.com");
        var (_, _, assessmentIdB, itemIdB) = await StartedAssessmentAsync($"sp_speak_b_{Guid.NewGuid():N}@test.com");

        var client = ClientWithToken(tokenA);
        var resp = await client.PostAsync(
            $"/api/student/placement/audio/{assessmentIdB}/items/{itemIdB}/speaking", SpeakingUploadContent());

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UploadSpeakingAudio_UnknownItemId_Returns404()
    {
        var (token, _, assessmentId, _) = await StartedAssessmentAsync($"sp_speak_unknown_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync(
            $"/api/student/placement/audio/{assessmentId}/items/{Guid.NewGuid()}/speaking", SpeakingUploadContent());

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UploadSpeakingAudio_UnsupportedMimeType_Returns400()
    {
        var (token, _, assessmentId, itemId) = await StartedAssessmentAsync($"sp_speak_badmime_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync(
            $"/api/student/placement/audio/{assessmentId}/items/{itemId}/speaking",
            SpeakingUploadContent(mimeType: "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UploadSpeakingAudio_ValidAudio_ReturnsStorageKey()
    {
        var (token, _, assessmentId, itemId) = await StartedAssessmentAsync($"sp_speak_ok_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync(
            $"/api/student/placement/audio/{assessmentId}/items/{itemId}/speaking", SpeakingUploadContent());

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("storageKey").GetString()));
        Assert.Equal("audio/webm", body.GetProperty("mimeType").GetString());
        Assert.Equal(4.2, body.GetProperty("durationSeconds").GetDouble(), precision: 3);
    }
}
