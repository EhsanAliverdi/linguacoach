using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Admin.StudentReadiness;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 20D: integration tests for the student pilot-readiness audit + repair endpoints.
/// Phase I2C: scenarios that seeded StudentActivityReadinessItem rows or exercised the
/// readiness-pool-specific repair actions (ExpireInvalidReadinessItems, ExpireStaleReservedItems)
/// were rewritten to use the surviving GenerateLearningPlanIfMissing action and the
/// learningplan.exists check instead — see
/// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed class AdminStudentReadinessEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminStudentReadinessEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetProfileIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        return profile!.Id;
    }

    [Fact]
    public async Task GetReadiness_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/admin/students/{Guid.NewGuid()}/readiness");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReadiness_AsStudent_Returns403()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync($"readiness403_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);
        var response = await ClientWithToken(token).GetAsync($"/api/admin/students/{profileId}/readiness");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetReadiness_AsAdmin_Returns200()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"readiness200_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);

        var response = await ClientWithToken(adminToken).GetAsync($"/api/admin/students/{profileId}/readiness");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("readyForPilot", out _));
        Assert.True(body.TryGetProperty("readinessStatus", out _));
        Assert.True(body.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetReadiness_UnknownStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync($"/api/admin/students/{Guid.NewGuid()}/readiness");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Repair_DryRun_ViaApi_Succeeds()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"readinessdryrun_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);

        var response = await ClientWithToken(adminToken).PostAsJsonAsync(
            $"/api/admin/students/{profileId}/readiness/repair",
            new { actionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing, dryRun = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("dryRun").GetBoolean());
    }

    [Fact]
    public async Task Repair_Real_WithoutReason_Returns400()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"readinessnoreason_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);

        var response = await ClientWithToken(adminToken).PostAsJsonAsync(
            $"/api/admin/students/{profileId}/readiness/repair",
            new { actionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing, dryRun = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Repair_Real_WithReason_ImprovesSubsequentReadiness()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"readinessrepair_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.FirstAsync(p => p.Id == profileId);
            profile.SetLifecycleStage(StudentLifecycleStage.CourseReady);
            typeof(StudentProfile).GetProperty(nameof(StudentProfile.OnboardingStatus))!
                .SetValue(profile, OnboardingStatus.Complete);
            await db.SaveChangesAsync();
        }

        var client = ClientWithToken(adminToken);
        var before = await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/{profileId}/readiness");
        var beforeCheck = before.GetProperty("checks").EnumerateArray()
            .First(c => c.GetProperty("key").GetString() == "learningplan.exists");
        Assert.Equal("fail", beforeCheck.GetProperty("status").GetString());

        var repairResponse = await client.PostAsJsonAsync(
            $"/api/admin/students/{profileId}/readiness/repair",
            new
            {
                actionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
                dryRun = false,
                reason = "Generating a missing plan for pilot test.",
            });
        Assert.Equal(HttpStatusCode.OK, repairResponse.StatusCode);

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/{profileId}/readiness");
        var afterCheck = after.GetProperty("checks").EnumerateArray()
            .First(c => c.GetProperty("key").GetString() == "learningplan.exists");
        Assert.Equal("pass", afterCheck.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetReadiness_ProductionLikeDuplicateActivityShape_Returns200WithStructuredChecks()
    {
        // TODO-20G-3 regression: the audit must return 200 with structured checks regardless of
        // unusual underlying data shape, never a raw 500.
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"readinessprodshape_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            for (var i = 0; i < 49; i++)
            {
                db.LearningActivities.Add(new LearningActivity(
                    ActivityType.ListeningComprehension, ActivitySource.AiGenerated, "Listening practice", "B2",
                    "{\"transcript\":\"hi\"}"));
            }

            await db.SaveChangesAsync();
        }

        var response = await ClientWithToken(adminToken).GetAsync($"/api/admin/students/{profileId}/readiness");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() > 0);
        // Every check must be a structured status, never a leaked raw exception message.
        foreach (var check in checks.EnumerateArray())
        {
            var message = check.GetProperty("message").GetString();
            Assert.DoesNotContain("StackTrace", message);
            Assert.DoesNotContain("System.", message);
        }
    }

    [Fact]
    public async Task Response_NeverContainsSecretsOrRawPrompts()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"readinessnoSecrets_{Guid.NewGuid():N}@t.com");
        var profileId = await GetProfileIdAsync(userId);

        var response = await ClientWithToken(adminToken).GetAsync($"/api/admin/students/{profileId}/readiness");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("apiKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system prompt", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bearer ", raw, StringComparison.OrdinalIgnoreCase);
    }
}
