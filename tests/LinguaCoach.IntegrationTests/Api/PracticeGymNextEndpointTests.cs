using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase I2A (legacy fallback deletion): the Practice Gym pre-generation pool
/// (<c>PracticeActivityCache</c>) and the on-demand AI-generation fallback (formerly
/// <c>/api/activity/next</c>, also removed) are both gone. GET /api/activity/practice-gym/next
/// now always honestly reports nothing available — Practice Gym content comes exclusively
/// through the H10 launch bridge / H7 module suggestions flow. See
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
/// </summary>
public sealed class PracticeGymNextEndpointTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public PracticeGymNextEndpointTests(ActivityTestFactory factory) => _factory = factory;

    [Fact]
    public async Task GetNext_WithExerciseType_ReturnsSafeNoActivity()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_type_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/practice-gym/next?exerciseType=listen_and_answer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasActivity").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("reason").GetString()));
    }

    [Fact]
    public async Task GetNext_WithSkill_ReturnsSafeNoActivity()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_skill_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/practice-gym/next?skill=listening");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasActivity").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("reason").GetString()));
    }

    [Fact]
    public async Task GetNext_WithNoSkillOrExerciseType_ReturnsSafeNoActivity()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_empty_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/practice-gym/next");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasActivity").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("reason").GetString()));
    }

    [Fact]
    public async Task GetNext_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/activity/practice-gym/next?skill=listening");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
