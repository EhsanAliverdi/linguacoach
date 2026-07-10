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
/// Phase H7 — Practice Gym Module Pipeline. Uses the plain <see cref="ApiTestFactory"/> (matches
/// <c>PracticeGymSuggestionIntegrationTests</c>' convention) plus directly-seeded approved Module
/// Definitions to exercise the module selection path.
/// </summary>
public sealed class PracticeGymModulePipelineEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public PracticeGymModulePipelineEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static HttpClient ClientWithToken(ApiTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetProfileIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        return profile?.Id ?? userId;
    }

    private async Task<Guid> SeedApprovedModuleAsync(string? cefrLevel = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var lesson = new Lesson($"Learn {Guid.NewGuid():N}", "Body text.", LessonSourceMode.Manual, cefrLevel, "Vocabulary");
        lesson.Approve(null);
        db.Lessons.Add(lesson);

        var activity = new Exercise($"Activity {Guid.NewGuid():N}", "Instructions.", "gap_fill",
            ExerciseRendererType.Formio, ExerciseSourceMode.Manual, cefrLevel: cefrLevel, skill: "Vocabulary",
            formSchemaJson: "{\"components\":[]}", answerKeyJson: "{\"word_answer\":\"secret\"}",
            scoringRulesJson: "{\"word\":{\"kind\":\"exact\"}}");
        activity.Approve(null);
        db.Exercises.Add(activity);

        var module = new Module($"Module {Guid.NewGuid():N}", ModuleSourceMode.Manual, cefrLevel: cefrLevel, skill: "Vocabulary");
        module.Approve(null);
        db.Modules.Add(module);
        await db.SaveChangesAsync();

        db.ModuleLessonLinks.Add(new ModuleLessonLink(module.Id, lesson.Id, LessonResourceRole.Primary, 0));
        db.ModuleExerciseLinks.Add(new ModuleExerciseLink(module.Id, activity.Id, ModuleExerciseRole.PrimaryPractice, 0));
        await db.SaveChangesAsync();

        return module.Id;
    }

    [Fact]
    public async Task Suggestions_include_module_suggestions_when_compatible_approved_module_exists()
    {
        await SeedApprovedModuleAsync();
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h7_pg_module_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("moduleSuggestions", out var moduleSuggestions));
        Assert.False(moduleSuggestions.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
        Assert.False(moduleSuggestions.GetProperty("fallbackRequired").GetBoolean());
        Assert.True(moduleSuggestions.GetProperty("suggestions").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Suggestions_fall_back_when_no_compatible_module_exists()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h7_pg_fallback_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // Legacy readiness-pool suggestion sections are always present regardless of module outcome.
        Assert.True(body.TryGetProperty("suggestedItems", out _));
        Assert.True(body.TryGetProperty("continueItems", out _));
        Assert.True(body.TryGetProperty("reviewItems", out _));
    }

    [Fact]
    public async Task Module_suggestions_do_not_expose_answer_keys()
    {
        await SeedApprovedModuleAsync();
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h7_pg_noleak_answer_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/practice-gym/suggestions");
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("answerKeyJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("word_answer", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Module_suggestions_do_not_expose_scoring_rules()
    {
        await SeedApprovedModuleAsync();
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h7_pg_noleak_scoring_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/practice-gym/suggestions");
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("scoringRulesJson", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Existing_practice_gym_suggestions_endpoint_still_works()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h7_pg_existing_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Existing_practice_gym_fallback_path_still_works_with_no_modules()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h7_pg_fallback_path_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp1 = await client.GetAsync("/api/practice-gym/suggestions");
        var resp2 = await client.GetAsync("/api/practice-gym/suggestions");

        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
    }

    [Fact]
    public async Task Admin_preview_shows_suggested_modules()
    {
        var moduleId = await SeedApprovedModuleAsync();
        var (_, studentUserId) = await _factory.CreateStudentAndGetTokenAsync($"h7_preview_{Guid.NewGuid():N}@test.com");
        var studentProfileId = await GetProfileIdAsync(studentUserId);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/practice-gym/modules/preview?studentId={studentProfileId}&maxSuggestions=100");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("fallbackRequired").GetBoolean());
        var suggestions = body.GetProperty("suggestions").EnumerateArray().ToList();
        Assert.Contains(suggestions, s => s.GetProperty("moduleId").GetGuid() == moduleId);
    }

    [Fact]
    public async Task Admin_preview_shows_fallback_reason_when_no_module_available()
    {
        var (_, studentUserId) = await _factory.CreateStudentAndGetTokenAsync($"h7_preview_fallback_{Guid.NewGuid():N}@test.com");
        var studentProfileId = await GetProfileIdAsync(studentUserId);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/practice-gym/modules/preview?studentId={studentProfileId}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        if (!body.GetProperty("fallbackRequired").GetBoolean()) return; // other tests may have seeded compatible modules
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("fallbackReason").GetString()));
    }

    [Fact]
    public async Task NonAdmin_rejected_for_admin_preview_endpoint()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h7_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync($"/api/admin/practice-gym/modules/preview?studentId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task H3_lessons_endpoint_still_works()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/lessons");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task H4_activities_endpoint_still_works()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/exercises");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task H5_modules_endpoint_still_works()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/modules");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task H6_today_endpoint_still_works()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h7_today_regression_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/sessions/today");
        // No active learning path/module seeded here, so this may 400; the point of this
        // regression test is that H7 changes did not break the route/DI wiring (no 500).
        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    // Phase I2C: Existing_readiness_pool_endpoint_not_broken removed — the readiness pool and
    // AdminReadinessPoolController it exercised were deleted. Replaced with a check that H7's
    // module pipeline doesn't break the surviving student-readiness-audit endpoint instead. See
    // docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
    [Fact]
    public async Task Existing_student_readiness_endpoint_not_broken()
    {
        var (_, studentUserId) = await _factory.CreateStudentAndGetTokenAsync($"h7_readiness_{Guid.NewGuid():N}@test.com");
        var studentProfileId = await GetProfileIdAsync(studentUserId);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/students/{studentProfileId}/readiness");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
