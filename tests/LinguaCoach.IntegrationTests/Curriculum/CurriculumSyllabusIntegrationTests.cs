using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Domain.Constants;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Curriculum;

/// <summary>
/// Integration tests for Phase 10K curriculum syllabus foundation.
/// Verifies seeder, query service, and admin read-only endpoints.
/// Does NOT test CEFR-aware routing or activity selection (belongs to 10L).
/// </summary>
public sealed class CurriculumSyllabusIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public CurriculumSyllabusIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Seeder: starter objectives are loaded ────────────────────────────────

    [Fact]
    public async Task Seeder_StarterObjectivesAreLoaded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var count = db.CurriculumObjectives.Count();
        Assert.True(count > 0, $"Expected seeded objectives, got {count}");
    }

    [Fact]
    public async Task Seeder_ContainsObjectivesForA1_A2_B1_B2()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        foreach (var level in new[] { "A1", "A2", "B1", "B2" })
        {
            var exists = db.CurriculumObjectives.Any(o => o.CefrLevel == level && o.IsActive);
            Assert.True(exists, $"No active seeded objectives found for CEFR level {level}");
        }
    }

    [Fact]
    public async Task Seeder_ContainsAtLeastOneExamInspiredObjective()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(db.CurriculumObjectives.Any(o => o.IsExamInspired));
    }

    [Fact]
    public async Task Seeder_ContainsAtLeastOneReviewableObjective()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(db.CurriculumObjectives.Any(o => o.IsReviewable));
    }

    [Fact]
    public async Task Seeder_ContainsWorkplaceObjective_NotDefaultForAll()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var workplaceOnly = db.CurriculumObjectives
            .Where(o => o.ContextTagsJson.Contains("workplace"))
            .ToList();
        var all = db.CurriculumObjectives.Count();

        Assert.True(workplaceOnly.Count > 0, "Expected at least one workplace objective");
        Assert.True(workplaceOnly.Count < all, "workplace should not be the default for all objectives");
    }

    [Fact]
    public async Task Seeder_ContainsNonWorkplaceGeneralObjective()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var generalEnglish = db.CurriculumObjectives
            .Any(o => o.ContextTagsJson.Contains(CurriculumContextTagConstants.GeneralEnglish));
        Assert.True(generalEnglish);
    }

    [Fact]
    public async Task Seeder_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var countBefore = db.CurriculumObjectives.Count();

        // Run seeder a second time — should upsert, not insert.
        await CurriculumObjectiveSeeder.SeedAsync(db, NullLogger.Instance);

        var countAfter = db.CurriculumObjectives.Count();
        Assert.Equal(countBefore, countAfter);
    }

    // ── Query service ────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryService_GetActiveObjectives_ReturnsResults()
    {
        using var scope = _factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<ICurriculumSyllabusQuery>();

        var objectives = await query.GetActiveObjectivesAsync();
        Assert.NotEmpty(objectives);
    }

    [Fact]
    public async Task QueryService_GetByCefr_A1_ReturnsOnlyA1()
    {
        using var scope = _factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<ICurriculumSyllabusQuery>();

        var objectives = await query.GetByCefrAsync("A1");
        Assert.All(objectives, o => Assert.Equal("A1", o.CefrLevel));
        Assert.NotEmpty(objectives);
    }

    [Fact]
    public async Task QueryService_GetCandidatesForStudent_NullCefr_FallsBackToA1()
    {
        using var scope = _factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<ICurriculumSyllabusQuery>();

        var candidates = await query.GetCandidatesForStudentAsync(null, [], []);
        Assert.NotEmpty(candidates);
        Assert.All(candidates, o => Assert.Equal("A1", o.CefrLevel));
    }

    // ── Admin endpoint ───────────────────────────────────────────────────────

    [Fact]
    public async Task AdminEndpoint_GetObjectives_ReturnsSeededList()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/admin/curriculum/objectives");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() > 0);
    }

    [Fact]
    public async Task AdminEndpoint_GetObjectiveByKey_ReturnsCorrectObjective()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/admin/curriculum/objectives/a1.speaking.greetings_introductions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("a1.speaking.greetings_introductions", body.GetProperty("key").GetString());
    }

    [Fact]
    public async Task AdminEndpoint_GetObjectiveByKey_UnknownKey_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/admin/curriculum/objectives/does.not.exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_GetObjectives_Unauthenticated_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/admin/curriculum/objectives");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
