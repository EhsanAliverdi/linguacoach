using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
        Assert.Equal("Your personalised plan is being prepared.", body.GetProperty("message").GetString());
        Assert.Equal("Document Controller", body.GetProperty("careerProfile").GetString());
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
