using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 20C: proves admin edits made through the real runtime-settings API actually change
/// the behavior of the real, DI-resolved services — not just the admin display. Complements
/// Phase 20B's AdminRuntimeSettingsEndpointTests (which cover the registry/API contract itself:
/// validation, locked gates, audit, secrets).
///
/// Phase I2A (legacy fallback deletion) then Phase I2C (readiness-pool removal): the
/// "practice-gym-review-scaffold-pilot" feature gate group and everything it toggled
/// (PracticeGymSuggestionService's ReviewItems, the readiness pool itself) are gone. The
/// effective-wiring test that exercised that group was removed with it — see
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md and
/// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md. The 403 coverage below
/// now uses "activity-feedback-policy", the only remaining group on the same backing store.
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
            "/api/admin/runtime-settings/feature-gates/activity-feedback-policy/settings",
            new
            {
                values = new Dictionary<string, object> { ["ActivityFeedback.TodayPolicy"] = "Required" },
                reason = "Should be forbidden.",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
