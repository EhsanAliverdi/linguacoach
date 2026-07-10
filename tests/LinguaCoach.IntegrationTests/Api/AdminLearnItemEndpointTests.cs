using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase H3 — Learn Item foundation admin endpoints.</summary>
public sealed class AdminLearnItemEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminLearnItemEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<Guid> SeedVocabularyAsync(string word = "resilient")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var source = new CefrResourceSource($"Learn Item Test Source {Guid.NewGuid():N}", "CC-BY-4.0",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        db.CefrResourceSources.Add(source);
        var entry = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "B2",
            ResourceBankItemContent.Serialize(new VocabularyContent(word, "adjective", "able to recover quickly")));
        db.ResourceBankItems.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/learn-items");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/learn-items");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_learn_items()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/learn-items");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Admin_can_generate_learn_item_from_resource_bank_row()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/learn-items/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var learnItem = body.GetProperty("learnItem");
        Assert.Equal("PendingReview", learnItem.GetProperty("reviewStatus").GetString());
        Assert.Equal("GeneratedFromResources", learnItem.GetProperty("sourceMode").GetString());
        Assert.Single(learnItem.GetProperty("links").EnumerateArray());
        var reviewRoute = body.GetProperty("reviewRoute").GetString();
        Assert.Contains(learnItem.GetProperty("id").GetGuid().ToString(), reviewRoute);
    }

    [Fact]
    public async Task Admin_can_approve_and_reject_learn_items()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var genResp = await client.PostAsJsonAsync("/api/admin/learn-items/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });
        var genBody = await genResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = genBody.GetProperty("learnItem").GetProperty("id").GetGuid();

        var approveResp = await client.PostAsJsonAsync($"/api/admin/learn-items/{id}/approve", new { notes = "Looks good" });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approveBody = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approveBody.GetProperty("reviewStatus").GetString());

        // A second Learn Item to test rejection independently.
        var genResp2 = await client.PostAsJsonAsync("/api/admin/learn-items/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });
        var genBody2 = await genResp2.Content.ReadFromJsonAsync<JsonElement>();
        var id2 = genBody2.GetProperty("learnItem").GetProperty("id").GetGuid();

        var rejectResp = await client.PostAsJsonAsync($"/api/admin/learn-items/{id2}/reject", new { reason = "Needs more detail" });
        Assert.Equal(HttpStatusCode.OK, rejectResp.StatusCode);
        var rejectBody = await rejectResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rejected", rejectBody.GetProperty("reviewStatus").GetString());
        Assert.Equal("Needs more detail", rejectBody.GetProperty("rejectionReason").GetString());
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

        await client.PostAsJsonAsync("/api/admin/learn-items/generate-from-resources", new
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

        var sourceName = $"H3 Regression Source {Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/admin/content-imports", new
        {
            sourceName,
            resourceType = "vocabulary",
            inputMode = "pasted_text",
            content = "regressionword",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
