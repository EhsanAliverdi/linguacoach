using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Practice Gym Activation sprint — tests for GET /api/activity/next?pattern=
/// Verifies that each supported pattern key returns a valid activity with that
/// exercisePatternKey set, and that invalid keys return a safe 400.
/// </summary>
public sealed class PatternKeyedActivityEndpointTests : IClassFixture<PatternEvaluationTestFactory>
{
    private readonly PatternEvaluationTestFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public PatternKeyedActivityEndpointTests(PatternEvaluationTestFactory factory) => _factory = factory;

    // ── phrase_match ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNext_WithPatternPhraseMatch_Returns200WithPatternKey()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pm_{Guid.NewGuid():N}@test.com");
        var body = await GetNextWithPattern(ClientWithToken(token), ExercisePatternKey.PhraseMatch);

        AssertActivityWithPatternKey(body, ExercisePatternKey.PhraseMatch);
    }

    // ── gap_fill_workplace_phrase ─────────────────────────────────────────────

    [Fact]
    public async Task GetNext_WithPatternGapFill_Returns200WithPatternKey()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"gf_{Guid.NewGuid():N}@test.com");
        var body = await GetNextWithPattern(ClientWithToken(token), ExercisePatternKey.GapFillWorkplacePhrase);

        AssertActivityWithPatternKey(body, ExercisePatternKey.GapFillWorkplacePhrase);
    }

    // ── email_reply ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNext_WithPatternEmailReply_Returns200WithPatternKey()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"er_{Guid.NewGuid():N}@test.com");
        var body = await GetNextWithPattern(ClientWithToken(token), ExercisePatternKey.EmailReply);

        AssertActivityWithPatternKey(body, ExercisePatternKey.EmailReply);
    }

    // ── teams_chat_simulation ─────────────────────────────────────────────────

    [Fact]
    public async Task GetNext_WithPatternTeamsChat_Returns200WithPatternKey()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"tc_{Guid.NewGuid():N}@test.com");
        var body = await GetNextWithPattern(ClientWithToken(token), ExercisePatternKey.TeamsChatSimulation);

        AssertActivityWithPatternKey(body, ExercisePatternKey.TeamsChatSimulation);
    }

    // ── activity is persisted with pattern key in DB ──────────────────────────

    [Fact]
    public async Task GetNext_WithPatternEmailReply_ActivityPersistedWithPatternKey()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"er_db_{Guid.NewGuid():N}@test.com");
        var body = await GetNextWithPattern(ClientWithToken(token), ExercisePatternKey.EmailReply);

        var activityId = Guid.Parse(body.GetProperty("activityId").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var activity = await db.LearningActivities.FindAsync(activityId);

        Assert.NotNull(activity);
        Assert.Equal(ExercisePatternKey.EmailReply, activity.ExercisePatternKey);
    }

    [Fact]
    public async Task GetNext_WithPatternPhraseMatch_UsesReadyPracticeCache()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"pm_cache_{Guid.NewGuid():N}@test.com");
        Guid profileId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.SingleAsync(p => p.UserId == userId);
            profileId = profile.Id;
            var activity = new LearningActivity(
                ActivityType.VocabularyPractice,
                ActivitySource.AiGenerated,
                "Cached phrase cards",
                "B1",
                """
                {
                  "title": "Cached phrase cards",
                  "learningGoal": "Learn useful workplace phrases.",
                  "instructions": "Study the phrases, then match them.",
                  "pairs": [
                    { "phrase": "follow up", "meaning": "ask again later", "context": "I will follow up tomorrow." }
                  ],
                  "teachingNote": "Use follow up when you ask again after waiting."
                }
                """,
                exercisePatternKey: ExercisePatternKey.PhraseMatch);
            db.LearningActivities.Add(activity);
            await db.SaveChangesAsync();

            var cache = new PracticeActivityCache(
                profile.Id,
                ExercisePatternKey.PhraseMatch,
                "B1",
                "intermediate_workplace",
                Guid.NewGuid().ToString("N"),
                learningActivityId: activity.Id,
                status: PracticeCacheStatus.Ready);
            db.PracticeActivityCache.Add(cache);
            await db.SaveChangesAsync();
        }

        var body = await GetNextWithPattern(ClientWithToken(token), ExercisePatternKey.PhraseMatch);

        Assert.Equal("Cached phrase cards", body.GetProperty("title").GetString());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var assigned = await verifyDb.PracticeActivityCache.SingleAsync(c =>
            c.StudentProfileId == profileId && c.PatternKey == ExercisePatternKey.PhraseMatch);
        Assert.Equal(PracticeCacheStatus.Assigned, assigned.Status);
    }

    // ── invalid pattern key → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task GetNext_WithInvalidPatternKey_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"bad_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/activity/next?pattern=nonexistent_pattern_key");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error").GetString();
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    // ── existing ?type= behaviour still works ─────────────────────────────────

    [Fact]
    public async Task GetNext_WithTypeWritingScenario_StillReturns200()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"type_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/activity/next?type=WritingScenario");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("writingScenario", body.GetProperty("activityType").GetString());
    }

    // ── unauthenticated ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetNext_WithPattern_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/activity/next?pattern={ExercisePatternKey.EmailReply}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<JsonElement> GetNextWithPattern(HttpClient client, string pattern)
    {
        var response = await client.GetAsync($"/api/activity/next?pattern={pattern}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertActivityWithPatternKey(JsonElement body, string expectedPatternKey)
    {
        var activityId = body.GetProperty("activityId").GetString();
        Assert.False(string.IsNullOrEmpty(activityId));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));

        var patternKeyEl = body.GetProperty("exercisePatternKey");
        Assert.Equal(JsonValueKind.String, patternKeyEl.ValueKind);
        Assert.Equal(expectedPatternKey, patternKeyEl.GetString());
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
