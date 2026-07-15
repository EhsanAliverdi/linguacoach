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
/// Phase H10 — Exercise Runtime Launch Path / Attempt Bridge. Uses the plain
/// <see cref="ApiTestFactory"/> (matches H7's <c>PracticeGymModulePipelineEndpointTests</c>
/// convention) plus directly-seeded approved Modules with a launch-eligible Activity
/// Definition (well-formed <c>ScoringRulesDocument</c>-shaped JSON, matching what
/// <c>ActivityGenerationService</c> actually produces).
/// </summary>
public sealed class ExerciseLaunchEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ExerciseLaunchEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static HttpClient ClientWithToken(ApiTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> SeedLaunchableModuleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var lesson = new Lesson($"Learn {Guid.NewGuid():N}", "Body text.", LessonSourceMode.Manual, "B1", "Vocabulary");
        lesson.Approve(null);
        db.Lessons.Add(lesson);

        var activity = new Exercise($"Activity {Guid.NewGuid():N}", "Type the missing word.", "gap_fill",
            ExerciseRendererType.Formio, ExerciseSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary", estimatedMinutes: 5,
            formSchemaJson: "{\"components\":[{\"key\":\"answer\",\"type\":\"textfield\"}]}",
            answerKeyJson: "{\"answer\":\"resilient\"}",
            scoringRulesJson: "{\"Components\":{\"answer\":{\"Kind\":\"text_normalized\",\"CorrectAnswer\":\"resilient\"}}}",
            lessonId: lesson.Id);
        activity.Approve(null);
        db.Exercises.Add(activity);

        var module = new Module($"Module {Guid.NewGuid():N}", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary");
        module.Approve(null);
        db.Modules.Add(module);
        await db.SaveChangesAsync();

        db.ModuleLessonLinks.Add(new ModuleLessonLink(module.Id, lesson.Id, LessonResourceRole.Primary, 0));
        db.ModuleExerciseLinks.Add(new ModuleExerciseLink(module.Id, activity.Id, ModuleExerciseRole.PrimaryPractice, 0));
        await db.SaveChangesAsync();

        return module.Id;
    }

    private async Task<Guid> SeedUnsupportedModuleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var lesson = new Lesson($"Learn {Guid.NewGuid():N}", "Body text.", LessonSourceMode.Manual, "B1", "Vocabulary");
        lesson.Approve(null);
        db.Lessons.Add(lesson);

        var activity = new Exercise($"Activity {Guid.NewGuid():N}", "Write a paragraph.", "short_answer",
            ExerciseRendererType.Formio, ExerciseSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary", estimatedMinutes: 5,
            formSchemaJson: "{\"components\":[{\"key\":\"answer\",\"type\":\"textarea\"}]}",
            scoringRulesJson: "{\"Components\":{\"answer\":{\"Kind\":\"text_normalized\",\"RequiresManualOrAiEvaluation\":true}}}",
            lessonId: lesson.Id);
        activity.Approve(null);
        db.Exercises.Add(activity);

        var module = new Module($"Module {Guid.NewGuid():N}", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary");
        module.Approve(null);
        db.Modules.Add(module);
        await db.SaveChangesAsync();

        db.ModuleLessonLinks.Add(new ModuleLessonLink(module.Id, lesson.Id, LessonResourceRole.Primary, 0));
        db.ModuleExerciseLinks.Add(new ModuleExerciseLink(module.Id, activity.Id, ModuleExerciseRole.PrimaryPractice, 0));
        await db.SaveChangesAsync();

        return module.Id;
    }

    [Fact]
    public async Task Start_succeeds_for_launchable_module_suggestion()
    {
        var moduleId = await SeedLaunchableModuleAsync();
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_start_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsync($"/api/practice-gym/module-suggestions/{moduleId}/start", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.True(body.GetProperty("learningActivityId").GetGuid() != Guid.Empty);
        Assert.True(body.GetProperty("canSubmit").GetBoolean());
    }

    [Fact]
    public async Task Start_returns_unsupported_state_for_non_launchable_module_suggestion()
    {
        var moduleId = await SeedUnsupportedModuleAsync();
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_unsupported_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsync($"/api/practice-gym/module-suggestions/{moduleId}/start", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("unsupportedReason").GetString()));
    }

    [Fact]
    public async Task Start_requires_authentication()
    {
        var moduleId = await SeedLaunchableModuleAsync();
        var client = _factory.CreateClient();

        var resp = await client.PostAsync($"/api/practice-gym/module-suggestions/{moduleId}/start", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Start_rejects_a_caller_with_no_student_profile()
    {
        var moduleId = await SeedLaunchableModuleAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsync($"/api/practice-gym/module-suggestions/{moduleId}/start", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Start_response_does_not_expose_answer_keys()
    {
        var moduleId = await SeedLaunchableModuleAsync();
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_noleak_answer_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsync($"/api/practice-gym/module-suggestions/{moduleId}/start", null);
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("answerKeyJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"resilient\":\"resilient\"", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Start_response_does_not_expose_scoring_rules()
    {
        var moduleId = await SeedLaunchableModuleAsync();
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_noleak_scoring_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsync($"/api/practice-gym/module-suggestions/{moduleId}/start", null);
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("scoringRulesJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CorrectAnswer", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Existing_practice_gym_suggestions_endpoint_still_works()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_pg_suggestions_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Existing_practice_gym_next_fallback_endpoint_still_works()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_pg_next_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/activity/practice-gym/next?skill=vocabulary");
        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [Fact]
    public async Task H5_modules_admin_endpoint_still_works()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/modules");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task H6_today_endpoint_still_works()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_today_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/sessions/today");
        // No active learning path seeded here — the regression check is that H10 didn't break
        // route/DI wiring (no 500), same convention as H7's own Today regression test.
        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [Fact]
    public async Task H7_practice_gym_module_suggestions_still_work()
    {
        await SeedLaunchableModuleAsync();
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_h7_regression_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("moduleSuggestions", out var moduleSuggestions));
        Assert.False(moduleSuggestions.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
    }

    [Fact]
    public async Task Admin_endpoints_remain_protected()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h10_admin_protect_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/admin/practice-gym/modules/preview?studentId=" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
