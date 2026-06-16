using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Practice Gym pre-generation pool — tests for GET /api/activity/practice-gym/next.
/// Covers pool-first selection, on-demand fallback, disabled/planned exclusion,
/// and skill vs exact exercise type lookups.
/// </summary>
public sealed class PracticeGymNextEndpointTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public PracticeGymNextEndpointTests(ActivityTestFactory factory) => _factory = factory;

    [Fact]
    public async Task GetNext_WithReadyPoolItemForExerciseType_ReturnsPoolSourceAndAssignsCache()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"pg_pool_type_{Guid.NewGuid():N}@test.com");
        Guid profileId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.SingleAsync(p => p.UserId == userId);
            profileId = profile.Id;

            var activity = new LearningActivity(
                ActivityType.ListeningComprehension,
                ActivitySource.AiGenerated,
                "Pool-backed listening activity",
                "B1",
                """{"title":"Pool-backed listening activity"}""",
                exercisePatternKey: ExercisePatternKey.ListenAndAnswer);
            db.LearningActivities.Add(activity);
            await db.SaveChangesAsync();

            var cache = new PracticeActivityCache(
                profile.Id,
                ExercisePatternKey.ListenAndAnswer,
                "B1",
                "intermediate_workplace",
                Guid.NewGuid().ToString("N"),
                learningActivityId: activity.Id,
                status: PracticeCacheStatus.Ready);
            db.PracticeActivityCache.Add(cache);
            await db.SaveChangesAsync();
        }

        var response = await ClientWithToken(token).GetAsync("/api/activity/practice-gym/next?exerciseType=listen_and_answer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("hasActivity").GetBoolean());
        Assert.Equal("pool", body.GetProperty("source").GetString());
        Assert.Equal("listen_and_answer", body.GetProperty("exerciseType").GetString());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var assigned = await verifyDb.PracticeActivityCache.SingleAsync(c =>
            c.StudentProfileId == profileId && c.PatternKey == ExercisePatternKey.ListenAndAnswer);
        Assert.Equal(PracticeCacheStatus.Assigned, assigned.Status);
    }

    [Fact]
    public async Task GetNext_WithReadyPoolItemForSkill_ReturnsPoolSource()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"pg_pool_skill_{Guid.NewGuid():N}@test.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.SingleAsync(p => p.UserId == userId);

            var activity = new LearningActivity(
                ActivityType.ListeningComprehension,
                ActivitySource.AiGenerated,
                "Pool-backed listening activity (skill)",
                "B1",
                """{"title":"Pool-backed listening activity (skill)"}""",
                exercisePatternKey: ExercisePatternKey.ListenAndAnswer);
            db.LearningActivities.Add(activity);
            await db.SaveChangesAsync();

            var cache = new PracticeActivityCache(
                profile.Id,
                ExercisePatternKey.ListenAndAnswer,
                "B1",
                "intermediate_workplace",
                Guid.NewGuid().ToString("N"),
                learningActivityId: activity.Id,
                status: PracticeCacheStatus.Ready);
            db.PracticeActivityCache.Add(cache);
            await db.SaveChangesAsync();
        }

        var response = await ClientWithToken(token).GetAsync("/api/activity/practice-gym/next?skill=listening");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("hasActivity").GetBoolean());
        Assert.Equal("pool", body.GetProperty("source").GetString());
        Assert.Equal("listening", body.GetProperty("primarySkill").GetString());
    }

    [Fact]
    public async Task GetNext_WithNoPoolItem_FallsBackToOnDemandGeneration()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_fallback_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/practice-gym/next?exerciseType=listen_and_answer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("hasActivity").GetBoolean());
        Assert.Equal("onDemandFallback", body.GetProperty("source").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("activityId").GetString()));
    }

    [Fact]
    public async Task GetNext_WithDisabledExerciseType_ReturnsSafeNoActivity()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_disabled_{Guid.NewGuid():N}@test.com");

        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
                var type = await db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "listen_and_answer");
                type.SetEnabled(false);
                await db.SaveChangesAsync();
            }

            var response = await ClientWithToken(token).GetAsync("/api/activity/practice-gym/next?exerciseType=listen_and_answer");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("hasActivity").GetBoolean());
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var type = await db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "listen_and_answer");
            type.SetEnabled(true);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetNext_WithUnknownExerciseType_ReturnsSafeNoActivity()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_unknown_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/practice-gym/next?exerciseType=nonexistent_future_format");

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

    // ── existing /api/activity/next compatibility (regression) ───────────────

    [Fact]
    public async Task ExistingGetNext_WithExerciseTypeQueryParam_StillWorks()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_compat_type_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/next?exerciseType=listen_and_answer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ExercisePatternKey.ListenAndAnswer, body.GetProperty("exercisePatternKey").GetString());
    }

    [Fact]
    public async Task GetNext_WithHighlightCorrectSummaryExerciseType_ReturnsOkWithStagedContent()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_hcs_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/next?exerciseType=highlight_correct_summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ExercisePatternKey.HighlightCorrectSummary, body.GetProperty("exercisePatternKey").GetString());
        Assert.Equal("highlightCorrectSummary", body.GetProperty("interactionMode").GetString());
        var content = body.GetProperty("contentJson").GetString();
        Assert.Contains("module_stage_v1", content);
    }

    [Fact]
    public async Task GetNext_WithHighlightIncorrectWordsExerciseType_ReturnsOkWithStagedContent()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_hiw_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/next?exerciseType=highlight_incorrect_words");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ExercisePatternKey.HighlightIncorrectWords, body.GetProperty("exercisePatternKey").GetString());
        Assert.Equal("highlightIncorrectWords", body.GetProperty("interactionMode").GetString());
        var content = body.GetProperty("contentJson").GetString();
        Assert.Contains("module_stage_v1", content);
    }

    [Fact]
    public async Task GetNext_WithSummarizeSpokenTextExerciseType_ReturnsOkWithStagedAudioAndPrompt()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_sst_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/next?exerciseType=summarize_spoken_text");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ExercisePatternKey.SummarizeSpokenText, body.GetProperty("exercisePatternKey").GetString());
        Assert.Equal("summarizeSpokenText", body.GetProperty("interactionMode").GetString());
        var content = body.GetProperty("contentJson").GetString();
        Assert.Contains("module_stage_v1", content);
        Assert.Contains("audioScript", content);
        Assert.Contains("prompt", content);
    }

    [Fact]
    public async Task ExistingGetNext_WithPatternQueryParam_StillWorks()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_compat_pattern_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync($"/api/activity/next?pattern={ExercisePatternKey.PhraseMatch}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ExercisePatternKey.PhraseMatch, body.GetProperty("exercisePatternKey").GetString());
    }

    [Fact]
    public async Task ExistingGetNext_WithTypeQueryParam_StillWorks()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pg_compat_legacytype_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/next?type=WritingScenario");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("writingScenario", body.GetProperty("activityType").GetString());
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
