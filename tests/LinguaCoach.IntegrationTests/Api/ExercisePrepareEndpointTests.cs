using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for POST /api/sessions/{sessionId}/exercises/{exerciseId}/prepare.
/// Verifies: activity creation, idempotency, ownership, Review step handling,
/// ExerciseKind→ActivityType mapping, and no duplicate rows.
/// </summary>
public sealed class ExercisePrepareEndpointTests : IClassFixture<SessionTestFactory>
{
    private readonly SessionTestFactory _factory;

    public ExercisePrepareEndpointTests(SessionTestFactory factory) => _factory = factory;

    // ── Auth guard ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Prepare_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/sessions/{Guid.NewGuid()}/exercises/{Guid.NewGuid()}/prepare", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Activity creation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Prepare_NonReviewExercise_CreatesAndAssignsActivity()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"prep_create_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var firstNonReview = exercises.First(e => e.GetProperty("kind").GetString() != "review");
        var exerciseId = firstNonReview.GetProperty("exerciseId").GetGuid();

        var resp = await client.PostAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("activityId").GetGuid());
        Assert.False(body.GetProperty("isReview").GetBoolean());
    }

    [Fact]
    public async Task Prepare_NonReviewExercise_SetsLearningActivityIdOnExercise()
    {
        var email = $"prep_fk_{Guid.NewGuid():N}@test.com";
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(email);
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var firstNonReview = exercises.First(e => e.GetProperty("kind").GetString() != "review");
        var exerciseId = firstNonReview.GetProperty("exerciseId").GetGuid();

        await client.PostAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var exercise = await db.SessionExercises.FirstAsync(e => e.Id == exerciseId);
        Assert.NotNull(exercise.LearningActivityId);
    }

    // ── Idempotency ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Prepare_CalledTwice_ReturnsSameActivityId()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"prep_idem_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var firstNonReview = exercises.First(e => e.GetProperty("kind").GetString() != "review");
        var exerciseId = firstNonReview.GetProperty("exerciseId").GetGuid();

        var url = $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare";
        var resp1 = await client.PostAsync(url, null);
        var resp2 = await client.PostAsync(url, null);

        var id1 = (await resp1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activityId").GetGuid();
        var id2 = (await resp2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activityId").GetGuid();
        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task Prepare_CalledTwice_DoesNotCreateDuplicateActivityRows()
    {
        var email = $"prep_nodup_{Guid.NewGuid():N}@test.com";
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(email);
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var firstNonReview = exercises.First(e => e.GetProperty("kind").GetString() != "review");
        var exerciseId = firstNonReview.GetProperty("exerciseId").GetGuid();

        var url = $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare";
        var r1 = await client.PostAsync(url, null);
        var activityId = (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activityId").GetGuid();

        await client.PostAsync(url, null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var count = await db.LearningActivities.CountAsync(a => a.Id == activityId);
        Assert.Equal(1, count);
    }

    // ── Ownership ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Prepare_WrongStudent_Returns403()
    {
        var (tokenA, _) = await _factory.CreateCourseReadyStudentAsync(
            $"prep_403a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateCourseReadyStudentAsync(
            $"prep_403b_{Guid.NewGuid():N}@test.com");

        // Student A creates a session.
        var (sessionId, exercises) = await GetSessionWithExercisesAsync(ClientWithToken(tokenA));
        var exerciseId = exercises[0].GetProperty("exerciseId").GetGuid();

        // Student B tries to prepare an exercise on Student A's session.
        var resp = await ClientWithToken(tokenB).PostAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Prepare_WrongExercise_Returns400()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"prep_badex_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, _) = await GetSessionWithExercisesAsync(client);

        var resp = await client.PostAsync(
            $"/api/sessions/{sessionId}/exercises/{Guid.NewGuid()}/prepare", null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Review step ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Prepare_ReviewExercise_ReturnsIsReviewTrue()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"prep_rev_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var reviewEx = exercises.FirstOrDefault(e => e.GetProperty("kind").GetString() == "review");
        if (reviewEx.ValueKind == JsonValueKind.Undefined)
            return; // Session template has no Review step — skip.

        var exerciseId = reviewEx.GetProperty("exerciseId").GetGuid();
        var resp = await client.PostAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare", null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isReview").GetBoolean());
        Assert.NotEqual(Guid.Empty, body.GetProperty("activityId").GetGuid());
    }

    [Fact]
    public async Task Prepare_ReviewExercise_IsIdempotent()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync(
            $"prep_revidem_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (sessionId, exercises) = await GetSessionWithExercisesAsync(client);
        var reviewEx = exercises.FirstOrDefault(e => e.GetProperty("kind").GetString() == "review");
        if (reviewEx.ValueKind == JsonValueKind.Undefined)
            return;

        var exerciseId = reviewEx.GetProperty("exerciseId").GetGuid();
        var url = $"/api/sessions/{sessionId}/exercises/{exerciseId}/prepare";

        var r1 = await client.PostAsync(url, null);
        var r2 = await client.PostAsync(url, null);

        var id1 = (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activityId").GetGuid();
        var id2 = (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activityId").GetGuid();
        Assert.Equal(id1, id2);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid SessionId, List<JsonElement> Exercises)> GetSessionWithExercisesAsync(
        HttpClient client)
    {
        var todayResp = await client.GetAsync("/api/sessions/today");
        var todayBody = await todayResp.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = todayBody.GetProperty("sessionId").GetGuid();

        var detailResp = await client.GetAsync($"/api/sessions/{sessionId}");
        var detailBody = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        var exercises = detailBody.GetProperty("exercises").EnumerateArray().ToList();
        return (sessionId, exercises);
    }
}

/// <summary>
/// Unit tests for ExerciseKind → ActivityType mapping.
/// Tests the static mapping method directly — no DB needed.
/// </summary>
public sealed class ExerciseKindMappingTests
{
    [Theory]
    [InlineData(ExerciseKind.VocabularyWarmup, ActivityType.VocabularyPractice)]
    [InlineData(ExerciseKind.ContextInput,     ActivityType.WritingScenario)]
    [InlineData(ExerciseKind.ListeningInput,   ActivityType.ListeningComprehension)]
    [InlineData(ExerciseKind.ReadingInput,     ActivityType.ReadingTask)]
    [InlineData(ExerciseKind.WritingTask,      ActivityType.WritingScenario)]
    [InlineData(ExerciseKind.SpeakingTask,     ActivityType.SpeakingRolePlay)]
    public void MapKindToActivityType_ReturnsExpectedType(ExerciseKind kind, ActivityType expected)
    {
        var result = ExercisePrepareHandler.MapKindToActivityType(kind);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapKindToActivityType_Review_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExercisePrepareHandler.MapKindToActivityType(ExerciseKind.Review));
    }
}
