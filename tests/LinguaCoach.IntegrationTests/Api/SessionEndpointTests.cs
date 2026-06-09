using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for SessionsController.
/// Seeds a CourseReady student with an active LearningPath + LearningModule.
/// All assertions use the deterministic template system — no real AI calls.
/// </summary>
public sealed class SessionEndpointTests : IClassFixture<SessionTestFactory>
{
    private readonly SessionTestFactory _factory;

    public SessionEndpointTests(SessionTestFactory factory) => _factory = factory;

    // ── Auth guard ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Today_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/sessions/today");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Start_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync($"/api/sessions/{Guid.NewGuid()}/start", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── GET /api/sessions/today ────────────────────────────────────────────────

    [Fact]
    public async Task Today_CourseReadyStudent_ReturnsSessionWithExercises()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_today_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/sessions/today");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("sessionId").GetGuid() != Guid.Empty);
        Assert.Equal("notStarted", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("exercises").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Today_CalledTwice_ReturnsSameSessionId()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_idem_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp1 = await client.GetAsync("/api/sessions/today");
        var resp2 = await client.GetAsync("/api/sessions/today");

        var body1 = await resp1.Content.ReadFromJsonAsync<JsonElement>();
        var body2 = await resp2.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            body1.GetProperty("sessionId").GetGuid(),
            body2.GetProperty("sessionId").GetGuid());
    }

    [Fact]
    public async Task Today_ExercisesAreOrdered_FirstIsVocabularyWarmup()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_order_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/sessions/today");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var exercises = body.GetProperty("exercises").EnumerateArray().ToList();
        Assert.NotEmpty(exercises);
        Assert.Equal(0, exercises[0].GetProperty("order").GetInt32());
        Assert.Equal("vocabularyWarmup", exercises[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Today_LastExerciseIsReview()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_review_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/sessions/today");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var exercises = body.GetProperty("exercises").EnumerateArray().ToList();
        Assert.NotEmpty(exercises);
        Assert.Equal("review", exercises[^1].GetProperty("kind").GetString());
    }

    // ── GET /api/sessions/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task GetById_OwnSession_ReturnsSessionDetail()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_getid_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var todayResp = await client.GetAsync("/api/sessions/today");
        var todayBody = await todayResp.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = todayBody.GetProperty("sessionId").GetGuid();

        var resp = await client.GetAsync($"/api/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(sessionId, body.GetProperty("sessionId").GetGuid());
        Assert.True(body.GetProperty("exercises").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetById_NonExistentSession_Returns404()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_404_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync($"/api/sessions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetById_AnotherStudentsSession_Returns403()
    {
        var (tokenA, _) = await _factory.CreateCourseReadyStudentAsync($"sess_403a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateCourseReadyStudentAsync($"sess_403b_{Guid.NewGuid():N}@test.com");

        // Student A creates a session.
        var todayResp = await ClientWithToken(tokenA).GetAsync("/api/sessions/today");
        var sessionId = (await todayResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("sessionId").GetGuid();

        // Student B tries to access it.
        var resp = await ClientWithToken(tokenB).GetAsync($"/api/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── POST /api/sessions/{id}/start ──────────────────────────────────────────

    [Fact]
    public async Task Start_NotStartedSession_ReturnsInProgress()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_start_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);

        var resp = await client.PostAsync($"/api/sessions/{sessionId}/start", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("inProgress", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("startedAtUtc").GetDateTime() > DateTime.MinValue);
    }

    [Fact]
    public async Task Start_TransitionsLifecycleToCourseReady_ToInLesson()
    {
        var email = $"sess_lifecycle_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await _factory.CreateCourseReadyStudentAsync(email);
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        await client.PostAsync($"/api/sessions/{sessionId}/start", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(StudentLifecycleStage.InLesson, profile.LifecycleStage);
    }

    [Fact]
    public async Task Start_AlreadyStarted_IsIdempotent()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_idem_start_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        var resp1 = await client.PostAsync($"/api/sessions/{sessionId}/start", null);
        var resp2 = await client.PostAsync($"/api/sessions/{sessionId}/start", null);

        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        var body1 = (await resp1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString();
        var body2 = (await resp2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString();
        Assert.Equal(body1, body2);
    }

    // ── POST /api/sessions/{id}/complete ───────────────────────────────────────

    [Fact]
    public async Task Complete_AfterStart_ReturnsCompleted()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_complete_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        await client.PostAsync($"/api/sessions/{sessionId}/start", null);

        var resp = await client.PostAsync($"/api/sessions/{sessionId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Complete_TransitionsLifecycleToActiveLearning()
    {
        var email = $"sess_active_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await _factory.CreateCourseReadyStudentAsync(email);
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        await client.PostAsync($"/api/sessions/{sessionId}/start", null);
        await client.PostAsync($"/api/sessions/{sessionId}/complete", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(StudentLifecycleStage.ActiveLearning, profile.LifecycleStage);
    }

    [Fact]
    public async Task Complete_AlreadyCompleted_IsIdempotent()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_idem_comp_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        await client.PostAsync($"/api/sessions/{sessionId}/start", null);
        var resp1 = await client.PostAsync($"/api/sessions/{sessionId}/complete", null);
        var resp2 = await client.PostAsync($"/api/sessions/{sessionId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
    }

    // ── POST /api/sessions/{id}/exercises/{eid}/complete ──────────────────────

    [Fact]
    public async Task CompleteExercise_FirstExercise_ReturnsCompletedAndSessionNotComplete()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_ex1_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        var exercises = await GetExercisesAsync(client, sessionId);
        var firstId = exercises[0].GetProperty("exerciseId").GetGuid();

        var resp = await client.PostAsync($"/api/sessions/{sessionId}/exercises/{firstId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", body.GetProperty("status").GetString());

        // Only one of N exercises complete — session is not done yet (unless N=1)
        if (exercises.Count > 1)
            Assert.False(body.GetProperty("sessionComplete").GetBoolean());
    }

    [Fact]
    public async Task CompleteExercise_AllExercises_SessionCompleteIsTrue()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_exall_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        var exercises = await GetExercisesAsync(client, sessionId);

        JsonElement lastBody = default;
        foreach (var ex in exercises)
        {
            var exId = ex.GetProperty("exerciseId").GetGuid();
            var r = await client.PostAsync($"/api/sessions/{sessionId}/exercises/{exId}/complete", null);
            lastBody = await r.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.True(lastBody.GetProperty("sessionComplete").GetBoolean());
    }

    [Fact]
    public async Task CompleteExercise_AlreadyCompleted_IsIdempotent()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_exidem_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        var exercises = await GetExercisesAsync(client, sessionId);
        var firstId = exercises[0].GetProperty("exerciseId").GetGuid();

        var r1 = await client.PostAsync($"/api/sessions/{sessionId}/exercises/{firstId}/complete", null);
        var r2 = await client.PostAsync($"/api/sessions/{sessionId}/exercises/{firstId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }

    [Fact]
    public async Task CompleteExercise_NonExistentExercise_Returns400()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_exmiss_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        var resp = await client.PostAsync(
            $"/api/sessions/{sessionId}/exercises/{Guid.NewGuid()}/complete", null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── GET /api/sessions/{id}/reflection ─────────────────────────────────────

    [Fact]
    public async Task Reflection_Returns501()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_refl_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var sessionId = await GetTodaysSessionIdAsync(client);
        var resp = await client.GetAsync($"/api/sessions/{sessionId}/reflection");
        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetTodaysSessionIdAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/sessions/today");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("sessionId").GetGuid();
    }

    private async Task<List<JsonElement>> GetExercisesAsync(HttpClient client, Guid sessionId)
    {
        var resp = await client.GetAsync($"/api/sessions/{sessionId}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("exercises").EnumerateArray().ToList();
    }
}

/// <summary>
/// Test factory that seeds a student ready for session generation.
/// Uses ActivityTestFactory (FakeAiProvider) as the base so AI calls don't fail.
/// </summary>
public sealed class SessionTestFactory : ActivityTestFactory
{
    /// <summary>
    /// Creates a student with lifecycle = CourseReady + an active LearningPath + LearningModule,
    /// so the session generator can create today's session.
    /// </summary>
    public async Task<(string Token, Guid UserId)> CreateCourseReadyStudentAsync(
        string email = "sess_student@test.linguacoach.com")
    {
        await SeedPromptTemplateAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<
            Microsoft.AspNetCore.Identity.UserManager<LinguaCoach.Persistence.Identity.ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return (tokenSvc.GenerateToken(existing.Id, existing.Email!, existing.Role), existing.Id);

        var user = new LinguaCoach.Persistence.Identity.ApplicationUser
        {
            UserName = email, Email = email,
            Role = UserRole.Student,
            EmailConfirmed = true, MustChangePassword = false
        };
        await userManager.CreateAsync(user, "Student@1234");

        // Student profile — CourseReady, following the onboarding step order.
        var pair = db.LanguagePairs.First();
        var career = db.CareerProfiles.First();

        var profile = new StudentProfile(user.Id);
        profile.SetLanguagePair(pair);                 // Step: Language
        profile.SetSessionPreference(15);              // Step: Preference
        profile.SetCareerProfile(career);              // Step: Career
        profile.SetSkillFocus(SkillFocus.Writing);     // Step: Skill
        profile.SetLifecycleStage(StudentLifecycleStage.CourseReady);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        // Active learning path + one module
        var path = new LearningPath(profile.Id, "Workplace English", "Test path");
        db.LearningPaths.Add(path);
        await db.SaveChangesAsync();

        var module = new LearningModule(path.Id, "Softening manager requests", "Practice softening language.", 1);
        module.SetAdaptiveMetadata("softening_language", "Deterministic test module.", "B1", null);
        db.LearningModules.Add(module);
        await db.SaveChangesAsync();

        return (tokenSvc.GenerateToken(user.Id, user.Email!, user.Role), user.Id);
    }
}
