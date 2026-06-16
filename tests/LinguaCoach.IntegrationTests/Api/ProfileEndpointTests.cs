using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for GET /api/profile and PUT /api/profile/preferences (Phase 10G).
/// </summary>
public sealed class ProfileEndpointTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public ProfileEndpointTests(ActivityTestFactory factory) => _factory = factory;

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Auth guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PutPreferences_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PutAsJsonAsync("/api/profile/preferences", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── GET /api/profile ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_ReturnsProfile()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prof_get_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("profileId", out _));
        Assert.True(body.TryGetProperty("cefrLevel", out _));
        Assert.True(body.TryGetProperty("learningGoals", out var goalsEl));
        Assert.Equal(JsonValueKind.Array, goalsEl.ValueKind);
        Assert.True(body.TryGetProperty("focusAreas", out var areasEl));
        Assert.Equal(JsonValueKind.Array, areasEl.ValueKind);
    }

    // ── PUT /api/profile/preferences ─────────────────────────────────────────

    [Fact]
    public async Task PutPreferences_UpdatesStudentEditableFields()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prof_put_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var payload = new
        {
            preferredName = "Janie",
            supportLanguageCode = "fa",
            supportLanguageName = "Persian",
            translationHelpPreference = 1, // WhenDifficult
            learningGoals = new[] { "Day-to-day English", "Travel English" },
            customLearningGoal = "Aviation English",
            focusAreas = new[] { "Speaking", "Listening" },
            customFocusArea = "Interviews",
            difficultyPreference = 1, // Balanced
            preferredSessionDurationMinutes = 30,
        };

        var putResp = await client.PutAsJsonAsync("/api/profile/preferences", payload);
        Assert.Equal(HttpStatusCode.NoContent, putResp.StatusCode);

        // Verify via GET
        var getResp = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var body = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Janie", body.GetProperty("preferredName").GetString());
        Assert.Equal("fa", body.GetProperty("supportLanguageCode").GetString());
        Assert.Equal("Persian", body.GetProperty("supportLanguageName").GetString());
        Assert.Equal("WhenDifficult", body.GetProperty("translationHelpPreference").GetString());
        Assert.Equal("Balanced", body.GetProperty("difficultyPreference").GetString());
        Assert.Equal(30, body.GetProperty("preferredSessionDurationMinutes").GetInt32());

        var goals = body.GetProperty("learningGoals");
        Assert.Equal(2, goals.GetArrayLength());
    }

    [Fact]
    public async Task PutPreferences_CannotChangeCefrLevel()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"prof_cefr_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // GET initial CEFR (should be null)
        var before = await client.GetAsync("/api/profile");
        var beforeBody = await before.Content.ReadFromJsonAsync<JsonElement>();
        var initialCefr = beforeBody.GetProperty("cefrLevel").ValueKind == JsonValueKind.Null
            ? null
            : beforeBody.GetProperty("cefrLevel").GetString();

        // PUT with no cefrLevel field (not in the contract)
        var putResp = await client.PutAsJsonAsync("/api/profile/preferences", new
        {
            preferredName = "TestUser",
        });
        Assert.Equal(HttpStatusCode.NoContent, putResp.StatusCode);

        // CEFR must still match initial
        var after = await client.GetAsync("/api/profile");
        var afterBody = await after.Content.ReadFromJsonAsync<JsonElement>();
        var afterCefr = afterBody.GetProperty("cefrLevel").ValueKind == JsonValueKind.Null
            ? null
            : afterBody.GetProperty("cefrLevel").GetString();

        Assert.Equal(initialCefr, afterCefr);
    }
}
