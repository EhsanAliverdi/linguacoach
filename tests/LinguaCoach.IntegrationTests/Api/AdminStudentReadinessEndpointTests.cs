using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Admin.StudentReadiness;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 20D: integration tests for the student pilot-readiness audit + repair endpoints.
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
            new { actionKey = StudentReadinessRepairActions.ExpireStaleReservedItems, dryRun = true });

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
            new { actionKey = StudentReadinessRepairActions.ExpireStaleReservedItems, dryRun = false });

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
            var item = new StudentActivityReadinessItem(
                studentId: profileId, source: ReadinessPoolSource.PracticeGym, targetCefrLevel: "B2",
                routingReason: RoutingReason.Normal, isLowerLevelContent: false);
            db.StudentActivityReadinessItems.Add(item);
            item.MarkGenerating();
            item.MarkReady();
            item.Reserve();
            await db.SaveChangesAsync();
            typeof(StudentActivityReadinessItem).GetProperty(nameof(StudentActivityReadinessItem.ReservedAt))!
                .SetValue(item, DateTime.UtcNow.AddHours(-10));
            await db.SaveChangesAsync();
        }

        var client = ClientWithToken(adminToken);
        var before = await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/{profileId}/readiness");
        var beforeCheck = before.GetProperty("checks").EnumerateArray()
            .First(c => c.GetProperty("key").GetString() == "feedback.no_stuck_reserved");
        Assert.Equal("warning", beforeCheck.GetProperty("status").GetString());

        var repairResponse = await client.PostAsJsonAsync(
            $"/api/admin/students/{profileId}/readiness/repair",
            new
            {
                actionKey = StudentReadinessRepairActions.ExpireStaleReservedItems,
                dryRun = false,
                reason = "Cleaning up stale reservation for pilot test.",
            });
        Assert.Equal(HttpStatusCode.OK, repairResponse.StatusCode);

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/{profileId}/readiness");
        var afterCheck = after.GetProperty("checks").EnumerateArray()
            .First(c => c.GetProperty("key").GetString() == "feedback.no_stuck_reserved");
        Assert.Equal("pass", afterCheck.GetProperty("status").GetString());
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
