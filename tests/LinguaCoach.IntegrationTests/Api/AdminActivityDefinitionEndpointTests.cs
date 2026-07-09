using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase H4 — Activity foundation admin endpoints.</summary>
public sealed class AdminActivityDefinitionEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminActivityDefinitionEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<Guid> SeedVocabularyAsync(string word)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var source = new CefrResourceSource($"Activity Test Source {Guid.NewGuid():N}", "CC-BY-4.0",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        db.CefrResourceSources.Add(source);
        var entry = new CefrVocabularyEntry(source.Id, word, "B2", "adjective", "able to recover quickly");
        db.CefrVocabularyEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private async Task<Guid> GenerateLearnItemAsync(HttpClient client, Guid vocabId)
    {
        var resp = await client.PostAsJsonAsync("/api/admin/learn-items/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("learnItem").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/activities");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/activities");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_activities()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/activities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Admin_can_generate_activity_from_resource_bank_row()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/activities/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var activity = body.GetProperty("activity");
        Assert.Equal("PendingReview", activity.GetProperty("reviewStatus").GetString());
        Assert.Equal("GeneratedFromResources", activity.GetProperty("sourceMode").GetString());
        Assert.Equal("Formio", activity.GetProperty("rendererType").GetString());
        Assert.Single(activity.GetProperty("links").EnumerateArray());
    }

    [Fact]
    public async Task Admin_can_generate_activity_from_learn_item()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var learnItemId = await GenerateLearnItemAsync(client, vocabId);

        var response = await client.PostAsJsonAsync("/api/admin/activities/generate-from-learn-item", new
        {
            learnItemId,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var activity = body.GetProperty("activity");
        Assert.Equal(learnItemId, activity.GetProperty("learnItemId").GetGuid());
        Assert.Equal("GeneratedFromLearnItem", activity.GetProperty("sourceMode").GetString());
    }

    [Fact]
    public async Task Admin_can_approve_and_reject_activities()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var genResp = await client.PostAsJsonAsync("/api/admin/activities/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });
        var genBody = await genResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = genBody.GetProperty("activity").GetProperty("id").GetGuid();

        var approveResp = await client.PostAsJsonAsync($"/api/admin/activities/{id}/approve", new { notes = "Looks good" });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approveBody = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approveBody.GetProperty("reviewStatus").GetString());

        var genResp2 = await client.PostAsJsonAsync("/api/admin/activities/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });
        var genBody2 = await genResp2.Content.ReadFromJsonAsync<JsonElement>();
        var id2 = genBody2.GetProperty("activity").GetProperty("id").GetGuid();

        var rejectResp = await client.PostAsJsonAsync($"/api/admin/activities/{id2}/reject", new { reason = "Answer key wrong" });
        Assert.Equal(HttpStatusCode.OK, rejectResp.StatusCode);
        var rejectBody = await rejectResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rejected", rejectBody.GetProperty("reviewStatus").GetString());
    }

    [Fact]
    public async Task Generate_from_resources_does_not_publish_or_assign_anything()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");

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

        await client.PostAsJsonAsync("/api/admin/activities/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
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
    public async Task Existing_H1_resource_bank_endpoint_still_works()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/resource-bank");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Existing_H2_content_import_endpoint_still_works()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/content-imports", new
        {
            sourceName = $"H4 Regression Source {Guid.NewGuid():N}",
            resourceType = "vocabulary",
            inputMode = "pasted_text",
            content = "regressionword",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
}
