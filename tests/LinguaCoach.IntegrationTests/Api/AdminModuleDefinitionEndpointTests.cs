using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase H5 — Module Definition foundation admin endpoints.</summary>
public sealed class AdminModuleDefinitionEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminModuleDefinitionEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<(Guid LearnItemId, Guid ActivityId)> SeedApprovedLearnItemAndActivityAsync(
        string cefrLevel = "B1", string skill = "Vocabulary")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var learnItem = new LearnItem($"Learn {Guid.NewGuid():N}", "Body text.", LearnItemSourceMode.Manual, cefrLevel, skill);
        learnItem.Approve(null);
        db.LearnItems.Add(learnItem);

        var activity = new ActivityDefinition($"Activity {Guid.NewGuid():N}", "Instructions.", "gap_fill",
            ActivityRendererType.Formio, ActivitySourceMode.Manual, cefrLevel: cefrLevel, skill: skill);
        activity.Approve(null);
        db.ActivityDefinitions.Add(activity);

        await db.SaveChangesAsync();
        return (learnItem.Id, activity.Id);
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/modules");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/modules");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_modules()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/modules");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Admin_can_get_module_detail()
    {
        var (learnItemId, activityId) = await SeedApprovedLearnItemAndActivityAsync();
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResp = await client.PostAsJsonAsync("/api/admin/modules", new
        {
            title = "Grammar module",
            learnItemLinks = new[] { new { learnItemId, role = "Primary" } },
            activityLinks = new[] { new { activityDefinitionId = activityId, role = "PrimaryPractice" } },
        });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetGuid();

        var getResp = await client.GetAsync($"/api/admin/modules/{id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_manual_module()
    {
        var (learnItemId, activityId) = await SeedApprovedLearnItemAndActivityAsync();
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/modules", new
        {
            title = "Grammar module",
            learnItemLinks = new[] { new { learnItemId, role = "Primary" } },
            activityLinks = new[] { new { activityDefinitionId = activityId, role = "PrimaryPractice" } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingReview", body.GetProperty("reviewStatus").GetString());
    }

    [Fact]
    public async Task Admin_can_generate_module_from_selected_items()
    {
        var (learnItemId, activityId) = await SeedApprovedLearnItemAndActivityAsync();
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/modules/generate-from-items", new
        {
            learnItemLinks = new[] { new { learnItemId, role = "Primary" } },
            activityLinks = new[] { new { activityDefinitionId = activityId, role = "PrimaryPractice" } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var module = body.GetProperty("module");
        Assert.Equal("PendingReview", module.GetProperty("reviewStatus").GetString());
        Assert.Equal("GeneratedFromLearnAndActivities", module.GetProperty("sourceMode").GetString());
    }

    [Fact]
    public async Task Admin_can_approve_and_reject_modules()
    {
        var (learnItemId, activityId) = await SeedApprovedLearnItemAndActivityAsync();
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var genResp = await client.PostAsJsonAsync("/api/admin/modules/generate-from-items", new
        {
            learnItemLinks = new[] { new { learnItemId, role = "Primary" } },
            activityLinks = new[] { new { activityDefinitionId = activityId, role = "PrimaryPractice" } },
        });
        var genBody = await genResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = genBody.GetProperty("module").GetProperty("id").GetGuid();

        var approveResp = await client.PostAsJsonAsync($"/api/admin/modules/{id}/approve", new { notes = "Looks good" });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approveBody = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approveBody.GetProperty("reviewStatus").GetString());

        var (learnItemId2, activityId2) = await SeedApprovedLearnItemAndActivityAsync();
        var genResp2 = await client.PostAsJsonAsync("/api/admin/modules/generate-from-items", new
        {
            learnItemLinks = new[] { new { learnItemId = learnItemId2, role = "Primary" } },
            activityLinks = new[] { new { activityDefinitionId = activityId2, role = "PrimaryPractice" } },
        });
        var genBody2 = await genResp2.Content.ReadFromJsonAsync<JsonElement>();
        var id2 = genBody2.GetProperty("module").GetProperty("id").GetGuid();

        var rejectResp = await client.PostAsJsonAsync($"/api/admin/modules/{id2}/reject", new { reason = "Weak activity" });
        Assert.Equal(HttpStatusCode.OK, rejectResp.StatusCode);
        var rejectBody = await rejectResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rejected", rejectBody.GetProperty("reviewStatus").GetString());
    }

    [Fact]
    public async Task Generate_from_items_does_not_create_student_assignment_side_effects()
    {
        var (learnItemId, activityId) = await SeedApprovedLearnItemAndActivityAsync();

        int studentProfilesBefore, learningActivitiesBefore, learningModulesBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            studentProfilesBefore = await db.StudentProfiles.CountAsync();
            learningActivitiesBefore = await db.LearningActivities.CountAsync();
            learningModulesBefore = await db.LearningModules.CountAsync();
        }

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await client.PostAsJsonAsync("/api/admin/modules/generate-from-items", new
        {
            learnItemLinks = new[] { new { learnItemId, role = "Primary" } },
            activityLinks = new[] { new { activityDefinitionId = activityId, role = "PrimaryPractice" } },
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            Assert.Equal(studentProfilesBefore, await db.StudentProfiles.CountAsync());
            Assert.Equal(learningActivitiesBefore, await db.LearningActivities.CountAsync());
            Assert.Equal(learningModulesBefore, await db.LearningModules.CountAsync());
        }
    }

    [Fact]
    public async Task Existing_H3_learn_item_endpoints_still_work()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/learn-items");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Existing_H4_activities_endpoints_still_work()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/activities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Existing_H1_resource_bank_endpoint_still_works()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/resource-bank");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
