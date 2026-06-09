using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class OnboardingEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public OnboardingEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/onboarding/status ────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_NewStudent_ReturnsNoneAndNotComplete()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"status_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/onboarding/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("None", body.GetProperty("currentStep").GetString());
        Assert.False(body.GetProperty("isComplete").GetBoolean());
    }

    [Fact]
    public async Task GetStatus_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/onboarding/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── PATCH /api/onboarding — language step ─────────────────────────────────

    [Fact]
    public async Task Patch_LanguageStep_AdvancesToLanguage()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"lang_{Guid.NewGuid():N}@test.com");
        var langPairId = await GetFaEnPairIdAsync();
        var client = ClientWithToken(token);

        var response = await client.PatchAsJsonAsync("/api/onboarding",
            new { step = "language", languagePairId = langPairId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Language", body.GetProperty("lastCompletedStep").GetString());
        Assert.False(body.GetProperty("isComplete").GetBoolean());
    }

    // ── PATCH /api/onboarding — full happy path ───────────────────────────────

    [Fact]
    public async Task Patch_AllFourSteps_CompletesOnboarding()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"full_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var (langPairId, trackId, careerId) = await GetSeedIdsAsync();

        await client.PatchAsJsonAsync("/api/onboarding",
            new { step = "language", languagePairId = langPairId });
        await client.PatchAsJsonAsync("/api/onboarding",
            new { step = "track", learningTrackId = trackId });
        await client.PatchAsJsonAsync("/api/onboarding",
            new { step = "career", careerProfileId = careerId });
        var last = await client.PatchAsJsonAsync("/api/onboarding",
            new { step = "skill", skillFocus = 0 }); // 0 = Writing

        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        var body = await last.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isComplete").GetBoolean());
        Assert.Equal("Skill", body.GetProperty("lastCompletedStep").GetString());
    }

    [Fact]
    public async Task Post_CareerStep_WithFreeText_AdvancesAndPersistsCareerContext()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync($"career_text_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var (langPairId, trackId, _) = await GetSeedIdsAsync();

        await client.PostAsJsonAsync("/api/onboarding", new { step = "language", languagePairId = langPairId });
        await client.PostAsJsonAsync("/api/onboarding", new { step = "track", learningTrackId = trackId });

        var response = await client.PostAsJsonAsync("/api/onboarding",
            new { step = "career", careerContext = "Nurse" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Career", body.GetProperty("lastCompletedStep").GetString());
        Assert.False(body.GetProperty("isComplete").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        Assert.Equal("Nurse", profile.CareerContext);
        Assert.Null(profile.CareerProfileId);
    }

    [Fact]
    public async Task Post_CareerStep_WithCareerProfileId_StillWorks()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"career_profile_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var (langPairId, trackId, careerId) = await GetSeedIdsAsync();

        await client.PostAsJsonAsync("/api/onboarding", new { step = "language", languagePairId = langPairId });
        await client.PostAsJsonAsync("/api/onboarding", new { step = "track", learningTrackId = trackId });

        var response = await client.PostAsJsonAsync("/api/onboarding",
            new { step = "career", careerProfileId = careerId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Career", body.GetProperty("lastCompletedStep").GetString());
    }

    [Fact]
    public async Task Post_SkillStep_Listening_CompletesOnboarding()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync($"skill_listening_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var (langPairId, trackId, careerId) = await GetSeedIdsAsync();

        await client.PostAsJsonAsync("/api/onboarding", new { step = "language", languagePairId = langPairId });
        await client.PostAsJsonAsync("/api/onboarding", new { step = "track", learningTrackId = trackId });
        await client.PostAsJsonAsync("/api/onboarding", new { step = "career", careerProfileId = careerId });

        var response = await client.PostAsJsonAsync("/api/onboarding",
            new { step = "skill", skillFocus = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isComplete").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        Assert.Equal(SkillFocus.Listening, profile.SkillFocus);
    }

    [Fact]
    public async Task Post_SkillStep_AcceptsNativeLanguageLearningGoal()
    {
        const string farsiGoal = "میخوام بتونم ایمیل رسمی بنویسم";
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync($"skill_farsi_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var (langPairId, trackId, careerId) = await GetSeedIdsAsync();

        await client.PostAsJsonAsync("/api/onboarding", new { step = "language", languagePairId = langPairId });
        await client.PostAsJsonAsync("/api/onboarding", new { step = "track", learningTrackId = trackId });
        await client.PostAsJsonAsync("/api/onboarding", new { step = "career", careerProfileId = careerId });

        var response = await client.PostAsJsonAsync("/api/onboarding",
            new { step = "skill", skillFocus = 0, learningGoalDescription = farsiGoal });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        Assert.Equal(farsiGoal, profile.LearningGoalDescription);
    }

    // ── PATCH /api/onboarding — out-of-order ─────────────────────────────────

    [Fact]
    public async Task Patch_TrackBeforeLanguage_Returns400()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"ooo_{Guid.NewGuid():N}@test.com");
        var (_, trackId, _) = await GetSeedIdsAsync();
        var client = ClientWithToken(token);

        var response = await client.PatchAsJsonAsync("/api/onboarding",
            new { step = "track", learningTrackId = trackId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── GET /api/dashboard — requires completed onboarding ───────────────────

    [Fact]
    public async Task Dashboard_BeforeOnboardingComplete_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"dash_early_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_AfterOnboardingComplete_Returns200WithMessage()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"dash_done_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        var (langPairId, trackId, careerId) = await GetSeedIdsAsync();

        await client.PatchAsJsonAsync("/api/onboarding", new { step = "language", languagePairId = langPairId });
        await client.PatchAsJsonAsync("/api/onboarding", new { step = "track", learningTrackId = trackId });
        await client.PatchAsJsonAsync("/api/onboarding", new { step = "career", careerProfileId = careerId });
        await client.PatchAsJsonAsync("/api/onboarding", new { step = "skill", skillFocus = 0 });

        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Complete your placement assessment to unlock your personalised course.", body.GetProperty("message").GetString());
        Assert.Equal("Document Controller", body.GetProperty("careerProfile").GetString());
        Assert.Equal("PlacementRequired", body.GetProperty("lifecycleStage").GetString());
        Assert.True(body.TryGetProperty("cefrLevel", out var cefrLevel));
        Assert.Equal(JsonValueKind.Null, cefrLevel.ValueKind);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetFaEnPairIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return db.LanguagePairs.First().Id;
    }

    private async Task<(Guid LangPairId, Guid TrackId, Guid CareerId)> GetSeedIdsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var pair = db.LanguagePairs.First();
        var track = db.LearningTracks.First();
        var career = db.CareerProfiles.First();
        return (pair.Id, track.Id, career.Id);
    }
}
