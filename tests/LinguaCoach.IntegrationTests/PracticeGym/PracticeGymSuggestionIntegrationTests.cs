using System.Net;
using System.Net.Http.Headers;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.IntegrationTests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.PracticeGym;

/// <summary>
/// Integration tests for Phase 10O Practice Gym suggestion service and API endpoints.
///
/// Phase I2A (legacy fallback deletion): SuggestedItems/ContinueItems/ReviewItems no longer
/// read the readiness pool for Practice-Gym-sourced rows — that generation path was removed. See
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
///
/// Phase I2C: the readiness pool itself was deleted, so tests that seeded
/// StudentActivityReadinessItem rows via IStudentActivityReadinessPoolService were removed —
/// there is nothing left to seed. /start and /complete are now permanently no-ops (see
/// PracticeGymSuggestionService's class doc comment); tests below assert that behavior directly.
/// See docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed class PracticeGymSuggestionIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public PracticeGymSuggestionIntegrationTests(ApiTestFactory factory)
        => _factory = factory;

    // 1. IPracticeGymSuggestionService is registered and resolves from DI.
    [Fact]
    public void SuggestionService_IsRegisteredInDI()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IPracticeGymSuggestionService>();
        Assert.NotNull(svc);
    }

    // 2. GET /api/practice-gym/suggestions returns 200 with empty sections when pool is empty.
    [Fact]
    public async Task GetSuggestions_EmptyPool_Returns200WithEmptySections()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync("suggestion-empty@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/practice-gym/suggestions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"suggestedItems\":[]", body.Replace(" ", ""));
        Assert.Contains("\"continueItems\":[]", body.Replace(" ", ""));
        Assert.Contains("\"reviewItems\":[]", body.Replace(" ", ""));
    }

    // 3. POST start is a permanent no-op — no readiness item can ever exist to reserve.
    [Fact]
    public async Task PostStart_AlwaysReturnsNotFound()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync("suggestion-start@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/practice-gym/suggestions/{Guid.NewGuid()}/start", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":false", body.Replace(" ", ""));
    }

    // 4. POST complete is a permanent no-op — always returns 204 without touching any data.
    [Fact]
    public async Task PostComplete_AlwaysReturnsNoContent()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync("suggestion-complete@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/practice-gym/suggestions/{Guid.NewGuid()}/complete", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // 5. Existing /api/activity/practice-gym/next smoke test — still works after Phase 10O.
    [Fact]
    public async Task ExistingPracticeGymNext_StillWorks()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync("suggestion-smoke@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Should return 200 (or 429 rate-limit) — not 404 or 500.
        var response = await client.GetAsync("/api/activity/practice-gym/next?skill=speaking");
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.TooManyRequests,
            $"Unexpected status: {response.StatusCode}");
    }

    // 6. Phase I2C: AdminReadinessPoolController (and every "/api/admin/readiness-pool/..."
    // route it exposed) was deleted along with the readiness pool. Confirms the route is
    // genuinely gone rather than merely read-only.
    [Fact]
    public async Task AdminReadinessPoolRoutes_AreGone()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var someId = Guid.NewGuid();
        var getResponse = await client.GetAsync($"/api/admin/students/{someId}/readiness-pool");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var postResponse = await client.PostAsync($"/api/admin/students/{someId}/readiness-pool", null);
        Assert.True(
            postResponse.StatusCode == HttpStatusCode.NotFound ||
            postResponse.StatusCode == HttpStatusCode.MethodNotAllowed,
            $"Expected no write endpoint, got: {postResponse.StatusCode}");
    }
}
