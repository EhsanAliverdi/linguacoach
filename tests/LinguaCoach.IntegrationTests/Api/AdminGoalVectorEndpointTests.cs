using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Adaptive Curriculum Sprint 3 — AdminGoalVectorController's one-time backfill trigger.
/// </summary>
public sealed class AdminGoalVectorEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminGoalVectorEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static HttpClient ClientWithToken(ApiTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Backfill_AsAdmin_ReturnsCounts()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/goal-vector/backfill-from-learning-goals", new { });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("studentsScanned").GetInt32() >= 0);
    }

    [Fact]
    public async Task Backfill_MappedLearningGoal_CreatesGoalWeight()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"gv_backfill_{Guid.NewGuid():N}@test.com");

        Guid profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
            profile.UpdateLearningPreferences(
                preferredName: null, supportLanguageCode: null, supportLanguageName: null,
                translationHelpPreference: null, learningGoals: ["travel"], customLearningGoal: null,
                focusAreas: null, customFocusArea: null, difficultyPreference: null, preferredSessionDurationMinutes: null);
            await db.SaveChangesAsync();
            profileId = profile.Id;
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);
        await client.PostAsJsonAsync("/api/admin/goal-vector/backfill-from-learning-goals", new { });

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var weight = await verifyDb.StudentGoalWeights.FirstOrDefaultAsync(g => g.StudentId == profileId && g.GoalTag == "travel");
        Assert.NotNull(weight);
    }

    [Fact]
    public async Task Backfill_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"gv_backfill_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsJsonAsync("/api/admin/goal-vector/backfill-from-learning-goals", new { });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
