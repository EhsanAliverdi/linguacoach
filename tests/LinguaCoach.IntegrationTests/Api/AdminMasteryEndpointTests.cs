using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for the mastery validation summary endpoint. Relocated from
/// ReviewScaffoldDryRunTests (Phase I2C readiness-pool removal — the dry-run half of that file
/// tested AdminReadinessPoolController.GetReviewScaffoldDryRun, which was deleted along with the
/// readiness pool; GetMasteryValidationSummary itself is unrelated and moved to
/// AdminMasteryController). See docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed class AdminMasteryEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminMasteryEndpointTests(ApiTestFactory factory)
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
    public async Task MasteryValidation_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/api/admin/mastery/validation-summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MasteryValidation_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"mastery403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/mastery/validation-summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MasteryValidation_AsAdmin_Returns200WithExpectedShape()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/mastery/validation-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        foreach (var field in new[]
        {
            "totalStudentsEvaluated", "totalObjectivesEvaluated",
            "countInsufficientEvidence", "countMastered", "countNeedsReview",
            "countNeedsPractice", "countAtRisk", "masteredExcludedFromNewLearning",
            "warnings", "generatedAt"
        })
        {
            Assert.True(body.TryGetProperty(field, out _), $"Missing field: {field}");
        }
    }

    // ── Skill Graph container/leaf Phase 3 (2026-07-27) — container mastery rollup ────────────────

    [Fact]
    public async Task ContainerRollup_NonExistentNode_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync($"/api/admin/mastery/students/{Guid.NewGuid()}/container-rollup/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ContainerRollup_ExistingContainerWithNoLeaves_ReturnsInsufficientEvidence()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        Guid containerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var container = new SkillGraphNode(
                $"grammar.rollup_endpoint_{Guid.NewGuid():N}.a1", "Rollup endpoint container", "D.", "A1", "grammar");
            db.SkillGraphNodes.Add(container);
            await db.SaveChangesAsync();
            containerId = container.Id;
        }

        var response = await ClientWithToken(adminToken)
            .GetAsync($"/api/admin/mastery/students/{Guid.NewGuid()}/container-rollup/{containerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(0, body.GetProperty("totalLeafCount").GetInt32());
        Assert.Equal("insufficientEvidence", body.GetProperty("rollupStatus").GetString());
    }

    [Fact]
    public async Task ContainerRollup_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"rollup403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .GetAsync($"/api/admin/mastery/students/{Guid.NewGuid()}/container-rollup/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
