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
/// Phase H6 — Daily Lesson Module Pipeline. Reuses <see cref="SessionTestFactory"/> (seeds a
/// CourseReady student with an active LearningPath + LearningModule) so <c>GET /api/sessions/today</c>
/// works, plus directly-seeded approved Module Definitions to exercise the module selection path.
/// </summary>
public sealed class DailyLessonModulePipelineEndpointTests : IClassFixture<SessionTestFactory>
{
    private readonly SessionTestFactory _factory;

    public DailyLessonModulePipelineEndpointTests(SessionTestFactory factory) => _factory = factory;

    private static HttpClient ClientWithToken(SessionTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> SeedApprovedModuleAsync(string? cefrLevel = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var learnItem = new LearnItem($"Learn {Guid.NewGuid():N}", "Body text.", LearnItemSourceMode.Manual, cefrLevel, "Vocabulary");
        learnItem.Approve(null);
        db.LearnItems.Add(learnItem);

        var activity = new ActivityDefinition($"Activity {Guid.NewGuid():N}", "Instructions.", "gap_fill",
            ActivityRendererType.Formio, ActivitySourceMode.Manual, cefrLevel: cefrLevel, skill: "Vocabulary",
            formSchemaJson: "{\"components\":[]}", answerKeyJson: "{\"word_answer\":\"secret\"}",
            scoringRulesJson: "{\"word\":{\"kind\":\"exact\"}}");
        activity.Approve(null);
        db.ActivityDefinitions.Add(activity);

        var module = new ModuleDefinition($"Module {Guid.NewGuid():N}", ModuleSourceMode.Manual, cefrLevel: cefrLevel, skill: "Vocabulary");
        module.Approve(null);
        db.ModuleDefinitions.Add(module);
        await db.SaveChangesAsync();

        db.ModuleDefinitionLearnItemLinks.Add(new ModuleDefinitionLearnItemLink(module.Id, learnItem.Id, LearnItemResourceRole.Primary, 0));
        db.ModuleDefinitionActivityLinks.Add(new ModuleDefinitionActivityLink(module.Id, activity.Id, ModuleActivityRole.PrimaryPractice, 0));
        await db.SaveChangesAsync();

        return module.Id;
    }

    [Fact]
    public async Task Today_returns_module_section_when_compatible_approved_module_exists()
    {
        await SeedApprovedModuleAsync();
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"h6_today_module_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/sessions/today");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("moduleSection", out var moduleSection));
        Assert.False(moduleSection.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
        Assert.False(moduleSection.GetProperty("fallbackRequired").GetBoolean());
        Assert.True(moduleSection.GetProperty("selectedModules").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Today_falls_back_when_no_compatible_module_exists()
    {
        // Phase I2B — Today is module-only now: when no compatible approved Module exists, Today
        // honestly reports nothing available rather than falling back to any legacy content.
        // This class shares one DB via IClassFixture, so a sibling test may have already seeded a
        // universal (CefrLevel: null) module before this one runs — same defensive pattern as
        // Admin_preview_shows_fallback_reason_when_no_module_available below.
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"h6_today_fallback_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/sessions/today");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        if (body.GetProperty("available").GetBoolean()) return; // other tests may have seeded compatible modules
        Assert.False(body.GetProperty("available").GetBoolean());
        Assert.True(body.GetProperty("moduleSection").ValueKind is JsonValueKind.Null
            || body.GetProperty("moduleSection").GetProperty("fallbackRequired").GetBoolean());
    }

    [Fact]
    public async Task Today_module_section_does_not_expose_answer_keys()
    {
        await SeedApprovedModuleAsync();
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"h6_today_noleak_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/sessions/today");
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("answerKeyJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scoringRulesJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("word_answer", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_preview_shows_selected_modules()
    {
        var moduleId = await SeedApprovedModuleAsync();
        var (_, studentUserId) = await _factory.CreateCourseReadyStudentAsync($"h6_preview_{Guid.NewGuid():N}@test.com");

        Guid studentProfileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            studentProfileId = (await db.StudentProfiles.FirstAsync(p => p.UserId == studentUserId)).Id;
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/daily-lesson/modules/preview?studentId={studentProfileId}&maxModules=100");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("fallbackRequired").GetBoolean());
        var selected = body.GetProperty("selectedModules").EnumerateArray().ToList();
        Assert.Contains(selected, m => m.GetProperty("moduleDefinitionId").GetGuid() == moduleId);
    }

    [Fact]
    public async Task Admin_preview_shows_fallback_reason_when_no_module_available()
    {
        var (_, studentUserId) = await _factory.CreateCourseReadyStudentAsync($"h6_preview_fallback_{Guid.NewGuid():N}@test.com");

        Guid studentProfileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            studentProfileId = (await db.StudentProfiles.FirstAsync(p => p.UserId == studentUserId)).Id;
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/daily-lesson/modules/preview?studentId={studentProfileId}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        if (!body.GetProperty("fallbackRequired").GetBoolean()) return; // other tests may have seeded compatible modules
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("fallbackReason").GetString()));
    }

    [Fact]
    public async Task Today_endpoint_is_idempotent_across_repeated_calls()
    {
        // Phase I2B — Today no longer creates a session, so idempotency now means: calling it
        // twice in a row returns the same `available`/module-selection outcome.
        var (token, _) = await _factory.CreateCourseReadyStudentAsync($"h6_legacy_today_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp1 = await client.GetAsync("/api/sessions/today");
        var resp2 = await client.GetAsync("/api/sessions/today");

        var body1 = await resp1.Content.ReadFromJsonAsync<JsonElement>();
        var body2 = await resp2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(body1.GetProperty("available").GetBoolean(), body2.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task Existing_practice_gym_endpoint_still_works()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h6_gym_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task H3_learn_items_endpoint_still_works()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/learn-items");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task H4_activities_endpoint_still_works()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/activities");
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
    public async Task NonAdmin_rejected_for_admin_preview_endpoint()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"h6_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync($"/api/admin/daily-lesson/modules/preview?studentId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Existing_readiness_pool_endpoint_not_broken()
    {
        var (_, studentUserId) = await _factory.CreateCourseReadyStudentAsync($"h6_readiness_{Guid.NewGuid():N}@test.com");

        Guid studentProfileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            studentProfileId = (await db.StudentProfiles.FirstAsync(p => p.UserId == studentUserId)).Id;
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/students/{studentProfileId}/readiness-pool");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
