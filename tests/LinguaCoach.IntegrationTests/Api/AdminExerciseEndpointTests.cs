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

/// <summary>Phase H4 — Activity foundation admin endpoints.</summary>
public sealed class AdminExerciseEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminExerciseEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<Guid> SeedVocabularyAsync(string word)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var source = new CefrResourceSource($"Activity Test Source {Guid.NewGuid():N}", "CC-BY-4.0",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        db.CefrResourceSources.Add(source);
        var entry = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "B2",
            ResourceBankItemContent.Serialize(new VocabularyContent(word, "adjective", "able to recover quickly")));
        db.ResourceBankItems.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private async Task<Guid> GenerateLessonAsync(HttpClient client, Guid vocabId)
    {
        var resp = await client.PostAsJsonAsync("/api/admin/lessons/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("lesson").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/exercises");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/exercises");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_activities()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/exercises");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
    }

    // Phase 2 (2026-07-15 exercise pipeline boundary consolidation) — the direct
    // "generate-from-resources" endpoint (no Lesson context) was removed entirely; every Exercise
    // now requires a Lesson (Resource Bank → Lesson → Exercise is the only supported flow).
    [Fact]
    public async Task Generate_from_resources_endpoint_no_longer_exists()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/exercises/generate-from-resources", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected the removed endpoint to be unreachable, got {response.StatusCode}.");
    }

    [Fact]
    public async Task Generate_from_resources_ai_endpoint_no_longer_exists()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/exercises/generate-from-resources/ai", new
        {
            resources = new[] { new { resourceType = "Vocabulary", resourceId = vocabId, role = "Primary" } },
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected the removed endpoint to be unreachable, got {response.StatusCode}.");
    }

    [Fact]
    public async Task Admin_can_generate_activity_from_lesson()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var lessonId = await GenerateLessonAsync(client, vocabId);

        var response = await client.PostAsJsonAsync("/api/admin/exercises/generate-from-lesson", new
        {
            lessonId,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var activity = body.GetProperty("activity");
        Assert.Equal(lessonId, activity.GetProperty("lessonId").GetGuid());
        Assert.Equal("GeneratedFromLesson", activity.GetProperty("sourceMode").GetString());
    }

    [Fact]
    public async Task Admin_can_approve_and_reject_activities()
    {
        var vocabId = await SeedVocabularyAsync($"word-{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var lessonId = await GenerateLessonAsync(client, vocabId);

        var genResp = await client.PostAsJsonAsync("/api/admin/exercises/generate-from-lesson", new { lessonId });
        var genBody = await genResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = genBody.GetProperty("activity").GetProperty("id").GetGuid();

        var approveResp = await client.PostAsJsonAsync($"/api/admin/exercises/{id}/approve", new { notes = "Looks good" });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approveBody = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approveBody.GetProperty("reviewStatus").GetString());

        var genResp2 = await client.PostAsJsonAsync("/api/admin/exercises/generate-from-lesson", new { lessonId });
        var genBody2 = await genResp2.Content.ReadFromJsonAsync<JsonElement>();
        var id2 = genBody2.GetProperty("activity").GetProperty("id").GetGuid();

        var rejectResp = await client.PostAsJsonAsync($"/api/admin/exercises/{id2}/reject", new { reason = "Answer key wrong" });
        Assert.Equal(HttpStatusCode.OK, rejectResp.StatusCode);
        var rejectBody = await rejectResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rejected", rejectBody.GetProperty("reviewStatus").GetString());
    }

    [Fact]
    public async Task Generate_from_lesson_does_not_publish_or_assign_anything()
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
        var lessonId = await GenerateLessonAsync(client, vocabId);

        await client.PostAsJsonAsync("/api/admin/exercises/generate-from-lesson", new { lessonId });

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
    public async Task Existing_H3_lesson_endpoints_still_work()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/lessons");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
