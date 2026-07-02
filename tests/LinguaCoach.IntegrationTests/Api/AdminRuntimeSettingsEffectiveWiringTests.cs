using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 20C: proves admin edits made through the real runtime-settings API actually change
/// the behavior of the real, DI-resolved ReadinessPool/PracticeGym services — not just the
/// admin display. Complements Phase 20B's AdminRuntimeSettingsEndpointTests (which cover the
/// registry/API contract itself: validation, locked gates, audit, secrets).
/// </summary>
public sealed class AdminRuntimeSettingsEffectiveWiringTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminRuntimeSettingsEffectiveWiringTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Update_PracticeGymPilotEnabled_MakesApprovedScaffoldItemVisible()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (studentToken, userId) = await _factory.CreateStudentAndGetTokenAsync($"wiring-pilot-on_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);
        await CreateApprovedScaffoldItemAsync(profileId);

        // Before enabling the pilot, the approved scaffold item must stay hidden.
        var before = await ClientWithToken(studentToken).GetAsync("/api/practice-gym/suggestions");
        Assert.DoesNotContain("\"reviewItems\":[{", (await before.Content.ReadAsStringAsync()).Replace(" ", ""));

        var update = await ClientWithToken(adminToken).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object> { ["ReadinessPool.PracticeGymPilotEnabled"] = true },
                reason = "Effective-wiring test: enable pilot.",
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var after = await ClientWithToken(studentToken).GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Contains("\"reviewItems\":[{", (await after.Content.ReadAsStringAsync()).Replace(" ", ""));
    }

    [Fact]
    public async Task Update_PracticeGymPilotLabelAndReason_ReflectedInSuggestionDto()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (studentToken, userId) = await _factory.CreateStudentAndGetTokenAsync($"wiring-pilot-label_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);
        await CreateApprovedScaffoldItemAsync(profileId);

        await ClientWithToken(adminToken).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object>
                {
                    ["ReadinessPool.PracticeGymPilotEnabled"] = true,
                    ["ReadinessPool.PracticeGymPilotLabel"] = "Custom Label",
                    ["ReadinessPool.PracticeGymPilotReason"] = "Custom reason text.",
                },
                reason = "Effective-wiring test: custom label/reason.",
            });

        var response = await ClientWithToken(studentToken).GetAsync("/api/practice-gym/suggestions");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Custom Label", body);
        Assert.Contains("Custom reason text.", body);
    }

    [Fact]
    public async Task ResetOverride_RestoresAppSettingsDefault_HidesScaffoldItemAgain()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (studentToken, userId) = await _factory.CreateStudentAndGetTokenAsync($"wiring-pilot-reset_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);
        await CreateApprovedScaffoldItemAsync(profileId);
        var client = ClientWithToken(adminToken);

        await client.PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object> { ["ReadinessPool.PracticeGymPilotEnabled"] = true },
                reason = "Enable before reset test.",
            });

        var enabled = await ClientWithToken(studentToken).GetAsync("/api/practice-gym/suggestions");
        Assert.Contains("\"reviewItems\":[{", (await enabled.Content.ReadAsStringAsync()).Replace(" ", ""));

        var resetRequest = new HttpRequestMessage(
            HttpMethod.Delete, "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/override")
        {
            Content = JsonContent.Create(new { reason = "Reverting to appsettings default." }),
        };
        var resetResponse = await client.SendAsync(resetRequest);
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var afterReset = await ClientWithToken(studentToken).GetAsync("/api/practice-gym/suggestions");
        Assert.DoesNotContain("\"reviewItems\":[{", (await afterReset.Content.ReadAsStringAsync()).Replace(" ", ""));
    }

    [Fact]
    public async Task Update_LessonGenerationBufferSetting_ReflectedInDatabaseRow()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/lesson-generation-buffer/settings",
            new
            {
                values = new Dictionary<string, object> { ["LessonGeneration.ReadyLessonBufferSize"] = 9 },
                reason = "Effective-wiring test: buffer size.",
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var settings = await db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync();
        Assert.NotNull(settings);
        Assert.Equal(9, settings!.ReadyLessonBufferSize);
    }

    [Fact]
    public async Task Update_AsStudent_Returns403()
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync($"wiring-student403_{Guid.NewGuid():N}@t.com");

        var response = await ClientWithToken(studentToken).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object> { ["ReadinessPool.PracticeGymPilotEnabled"] = true },
                reason = "Should be forbidden.",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- helpers ---

    private async Task<Guid> GetProfileIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        return profile?.Id ?? userId;
    }

    /// <summary>Creates a Review-routed, admin-approved scaffold item — pilot-gated visibility.</summary>
    private async Task CreateApprovedScaffoldItemAsync(Guid profileId)
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var id = await poolSvc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId = profileId,
            Source = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel = "B1",
            RoutingReason = RoutingReason.Review,
            IsLowerLevelContent = true,
            ContextTagsJson = "[\"general_english\"]",
            GeneratedBy = "integration-test",
            RequiresAdminReview = true,
        });
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);
        await poolSvc.MarkReviewOnlyAsync(id, "passed objective");

        var item = await db.StudentActivityReadinessItems.FirstAsync(i => i.Id == id);
        item.ApproveAdminReview(Guid.NewGuid());
        await db.SaveChangesAsync();
    }
}
