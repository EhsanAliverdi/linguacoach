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
///
/// Phase I2B — Today is module-only now: GET /api/sessions/today no longer creates a
/// LearningSession, and GET /api/sessions/{id} + the exercise /prepare action were deleted along
/// with the legacy generation pipeline (their only frontend caller, the lesson-runner page, was
/// also removed). Today's tests below assert the new honest `available`/`moduleSection` shape.
/// Start/Complete/CompleteExercise/Reflection still operate on legitimate LearningSession/
/// SessionExercise data via SessionLifecycleHandler (unaffected by this pass), so those tests now
/// seed a session directly through the DbContext instead of obtaining one from Today.
/// See docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md.
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
    public async Task Today_CourseReadyStudent_ReturnsHonestShape()
    {
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_today_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/sessions/today");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("available", out _));
        Assert.True(body.TryGetProperty("moduleSection", out _));
    }

    [Fact]
    public async Task Today_NoCompatibleModule_ReturnsNotAvailable()
    {
        // No Module is seeded for this fixture's CEFR/skill, so the bank-first
        // Daily Lesson Module selector has nothing to offer — Today must report this honestly
        // rather than falling back to any legacy or AI-generated content.
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"sess_notavail_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/sessions/today");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("available").GetBoolean());
    }

    // ── POST /api/sessions/{id}/start ──────────────────────────────────────────

    [Fact]
    public async Task Start_NotStartedSession_ReturnsInProgress()
    {
        var (token, _, sessionId, _) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_start_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync($"/api/sessions/{sessionId}/start", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("inProgress", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("startedAtUtc").GetDateTime() > DateTime.MinValue);
    }

    [Fact]
    public async Task Start_TransitionsLifecycleToCourseReady_ToInLesson()
    {
        var (token, userId, sessionId, _) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_lifecycle_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        await client.PostAsync($"/api/sessions/{sessionId}/start", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(StudentLifecycleStage.InLesson, profile.LifecycleStage);
    }

    [Fact]
    public async Task Start_AlreadyStarted_IsIdempotent()
    {
        var (token, _, sessionId, _) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_idem_start_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

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
        var (token, _, sessionId, _) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_complete_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        await client.PostAsync($"/api/sessions/{sessionId}/start", null);

        var resp = await client.PostAsync($"/api/sessions/{sessionId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Complete_TransitionsLifecycleToActiveLearning()
    {
        var (token, userId, sessionId, _) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_active_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

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
        var (token, _, sessionId, _) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_idem_comp_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

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
        var (token, _, sessionId, exerciseIds) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_ex1_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync($"/api/sessions/{sessionId}/exercises/{exerciseIds[0]}/complete", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", body.GetProperty("status").GetString());

        // Only one of N exercises complete — session is not done yet (unless N=1)
        if (exerciseIds.Count > 1)
            Assert.False(body.GetProperty("sessionComplete").GetBoolean());
    }

    [Fact]
    public async Task CompleteExercise_AllExercises_SessionCompleteIsTrue()
    {
        var (token, _, sessionId, exerciseIds) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_exall_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        JsonElement lastBody = default;
        foreach (var exId in exerciseIds)
        {
            var r = await client.PostAsync($"/api/sessions/{sessionId}/exercises/{exId}/complete", null);
            lastBody = await r.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.True(lastBody.GetProperty("sessionComplete").GetBoolean());
    }

    [Fact]
    public async Task CompleteExercise_AlreadyCompleted_IsIdempotent()
    {
        var (token, _, sessionId, exerciseIds) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_exidem_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var r1 = await client.PostAsync($"/api/sessions/{sessionId}/exercises/{exerciseIds[0]}/complete", null);
        var r2 = await client.PostAsync($"/api/sessions/{sessionId}/exercises/{exerciseIds[0]}/complete", null);

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }

    [Fact]
    public async Task CompleteExercise_NonExistentExercise_Returns400()
    {
        var (token, _, sessionId, _) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_exmiss_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync(
            $"/api/sessions/{sessionId}/exercises/{Guid.NewGuid()}/complete", null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── GET /api/sessions/{id}/reflection ─────────────────────────────────────

    [Fact]
    public async Task Reflection_Returns501()
    {
        var (token, _, sessionId, _) = await _factory.CreateCourseReadyStudentWithSessionAsync(
            $"sess_refl_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

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
}

/// <summary>
/// Test factory that seeds a CourseReady student, optionally with a historical LearningSession +
/// SessionExercises seeded directly through the DbContext.
///
/// Phase I2B — Today no longer generates LearningSession/SessionExercise rows, so tests that need
/// one (for Start/Complete/CompleteExercise/Reflection, which still operate on this legacy shape)
/// seed it directly rather than obtaining it from GET /api/sessions/today.
/// Uses ActivityTestFactory (FakeAiProvider) as the base so AI calls don't fail.
/// </summary>
public sealed class SessionTestFactory : ActivityTestFactory
{
    /// <summary>Creates a student with lifecycle = CourseReady + an active LearningPath + LearningModule.</summary>
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

    /// <summary>
    /// Creates a CourseReady student (as above) plus a historical LearningSession with two ordered
    /// SessionExercises, seeded directly — mirrors what LessonBatchGenerationJob used to produce,
    /// for tests exercising the still-live Start/Complete/CompleteExercise/Reflection endpoints.
    /// </summary>
    public async Task<(string Token, Guid UserId, Guid SessionId, List<Guid> ExerciseIds)>
        CreateCourseReadyStudentWithSessionAsync(string email)
    {
        var (token, userId) = await CreateCourseReadyStudentAsync(email);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        var module = await db.LearningModules.FirstAsync(m => m.LearningPathId ==
            db.LearningPaths.Where(p => p.StudentProfileId == profile.Id).Select(p => p.Id).First());

        var session = new LearningSession(
            module.Id, "Softening manager requests", "Workplace communication",
            "Practise softening language", 15, "Vocabulary", order: 0);
        db.LearningSessions.Add(session);
        await db.SaveChangesAsync();

        var exerciseIds = new List<Guid>();
        var ex1 = new SessionExercise(session.Id, 0, LinguaCoach.Domain.ExercisePatternKey.PhraseMatch, "Vocabulary", "Match the phrases.", 5);
        var ex2 = new SessionExercise(session.Id, 1, LinguaCoach.Domain.ExercisePatternKey.LessonReflection, "Vocabulary", "Reflect on the lesson.", 3);
        db.SessionExercises.AddRange(ex1, ex2);
        await db.SaveChangesAsync();
        exerciseIds.Add(ex1.Id);
        exerciseIds.Add(ex2.Id);

        return (token, userId, session.Id, exerciseIds);
    }
}
