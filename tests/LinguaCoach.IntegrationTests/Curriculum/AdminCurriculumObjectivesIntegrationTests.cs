using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Curriculum;

/// <summary>
/// Integration tests for Phase 10Q admin curriculum CRUD, activate/deactivate,
/// taxonomy, routing preview, and seeder behaviour.
/// </summary>
public sealed class AdminCurriculumObjectivesIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public AdminCurriculumObjectivesIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task AuthAsAdmin()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // ── List with filters ────────────────────────────────────────────────────

    [Fact]
    public async Task ListObjectives_NoFilter_ReturnsAllIncludingInactive()
    {
        await AuthAsAdmin();
        var response = await _client.GetAsync("/api/admin/curriculum/objectives");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ListObjectives_CefrFilter_ReturnsOnlyThatLevel()
    {
        await AuthAsAdmin();
        var response = await _client.GetAsync("/api/admin/curriculum/objectives?cefrLevel=A1&isActive=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var item in body.EnumerateArray())
            Assert.Equal("A1", item.GetProperty("cefrLevel").GetString());
    }

    [Fact]
    public async Task ListObjectives_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/admin/curriculum/objectives");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Taxonomy ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTaxonomy_ReturnsKnownCefrLevelsAndSkills()
    {
        await AuthAsAdmin();
        var response = await _client.GetAsync("/api/admin/curriculum/taxonomy");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var levels = body.GetProperty("cefrLevels").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("A1", levels);
        Assert.Contains("B2", levels);
        Assert.Contains("C2", levels);
        var skills = body.GetProperty("skills").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("writing", skills);
        Assert.Contains("speaking", skills);
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateObjective_ValidRequest_Returns201()
    {
        await AuthAsAdmin();
        var key = $"test.writing.integration_{Guid.NewGuid():N}";
        var request = new
        {
            key,
            title = "Test Writing Objective",
            description = "Integration test objective",
            cefrLevel = "B1",
            primarySkill = "writing",
            secondarySkills = new[] { "grammar" },
            contextTags = new[] { "general_english" },
            focusTags = new[] { "test_tag" },
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 999,
            difficultyBand = 2,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };

        var response = await _client.PostAsJsonAsync("/api/admin/curriculum/objectives", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(key, body.GetProperty("key").GetString());
        Assert.Equal("B1", body.GetProperty("cefrLevel").GetString());
        Assert.Equal("writing", body.GetProperty("primarySkill").GetString());
    }

    [Fact]
    public async Task CreateObjective_InvalidCefr_Returns400()
    {
        await AuthAsAdmin();
        var request = new
        {
            key = "test.bad.cefr",
            title = "Bad",
            description = "Bad cefr",
            cefrLevel = "Z9",
            primarySkill = "writing",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 0,
            difficultyBand = 1,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        var response = await _client.PostAsJsonAsync("/api/admin/curriculum/objectives", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateObjective_InvalidSkill_Returns400()
    {
        await AuthAsAdmin();
        var request = new
        {
            key = "test.bad.skill",
            title = "Bad",
            description = "Bad skill",
            cefrLevel = "A1",
            primarySkill = "flying",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 0,
            difficultyBand = 1,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        var response = await _client.PostAsJsonAsync("/api/admin/curriculum/objectives", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateObjective_SelfPrerequisite_Returns400()
    {
        await AuthAsAdmin();
        var key = "test.self.prereq";
        var request = new
        {
            key,
            title = "Self Prereq",
            description = "Should fail",
            cefrLevel = "A1",
            primarySkill = "speaking",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = new[] { key },
            recommendedOrder = 0,
            difficultyBand = 1,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        var response = await _client.PostAsJsonAsync("/api/admin/curriculum/objectives", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateObjective_DanglingPrerequisite_Returns400()
    {
        await AuthAsAdmin();
        var request = new
        {
            key = "test.dangling.prereq",
            title = "Dangling",
            description = "Should fail",
            cefrLevel = "A1",
            primarySkill = "speaking",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = new[] { "does.not.exist.anywhere" },
            recommendedOrder = 0,
            difficultyBand = 1,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        var response = await _client.PostAsJsonAsync("/api/admin/curriculum/objectives", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateObjective_NonAdmin_Returns403()
    {
        var (student, _) = await _factory.CreateStudentAndGetTokenAsync($"curriculum_403_{Guid.NewGuid():N}@test.com");
        var studentClient = _factory.CreateClient();
        studentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", student);

        var request = new
        {
            key = "test.student.write",
            title = "Should not work",
            description = "Student should not write curriculum",
            cefrLevel = "A1",
            primarySkill = "speaking",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 0,
            difficultyBand = 1,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        var response = await studentClient.PostAsJsonAsync("/api/admin/curriculum/objectives", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateObjective_ValidRequest_Returns200()
    {
        await AuthAsAdmin();
        // Create first
        var key = $"test.update.{Guid.NewGuid():N}";
        var createRequest = new
        {
            key,
            title = "Original Title",
            description = "Original",
            cefrLevel = "A1",
            primarySkill = "speaking",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 0,
            difficultyBand = 1,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        await _client.PostAsJsonAsync("/api/admin/curriculum/objectives", createRequest);

        // Update
        var updateRequest = new
        {
            key,
            title = "Updated Title",
            description = "Updated",
            cefrLevel = "A2",
            primarySkill = "vocabulary",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 10,
            difficultyBand = 2,
            isActive = true,
            isReviewable = true,
            isExamInspired = false,
            teachingNotes = "Updated notes",
            examplePrompts = (string?)null,
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/curriculum/objectives/{key}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Title", body.GetProperty("title").GetString());
        Assert.Equal("A2", body.GetProperty("cefrLevel").GetString());
    }

    [Fact]
    public async Task UpdateObjective_UnknownKey_Returns404()
    {
        await AuthAsAdmin();
        var request = new
        {
            key = "does.not.exist",
            title = "X",
            description = "Y",
            cefrLevel = "A1",
            primarySkill = "speaking",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 0,
            difficultyBand = 1,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        var response = await _client.PutAsJsonAsync("/api/admin/curriculum/objectives/does.not.exist", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Activate / Deactivate ─────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateAndReactivate_PreservesObjective()
    {
        await AuthAsAdmin();
        var key = $"test.lifecycle.{Guid.NewGuid():N}";
        var createRequest = new
        {
            key,
            title = "Lifecycle Test",
            description = "Test lifecycle",
            cefrLevel = "B1",
            primarySkill = "writing",
            secondarySkills = Array.Empty<string>(),
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 0,
            difficultyBand = 1,
            isActive = true,
            isReviewable = false,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        await _client.PostAsJsonAsync("/api/admin/curriculum/objectives", createRequest);

        // Deactivate
        var deactivate = await _client.PostAsync($"/api/admin/curriculum/objectives/{key}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await deactivate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(deactivated.GetProperty("isActive").GetBoolean());

        // Reactivate
        var activate = await _client.PostAsync($"/api/admin/curriculum/objectives/{key}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        var activated = await activate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(activated.GetProperty("isActive").GetBoolean());

        // DB row still exists
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(db.CurriculumObjectives.Any(o => o.Key == key));
    }

    [Fact]
    public async Task DeactivateObjective_UnknownKey_Returns404()
    {
        await AuthAsAdmin();
        var response = await _client.PostAsync("/api/admin/curriculum/objectives/does.not.exist/deactivate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Routing preview ───────────────────────────────────────────────────────

    [Fact]
    public async Task RoutingPreview_ReturnsRecommendation_DoesNotMutateState()
    {
        await AuthAsAdmin();
        var request = new
        {
            cefrLevelOverride = "B1",
            primarySkill = "writing",
            source = "admin_preview",
            allowReviewOrScaffold = false,
        };

        var response = await _client.PostAsJsonAsync("/api/admin/curriculum/routing-preview", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("targetCefrLevel", out var level));
        Assert.False(string.IsNullOrEmpty(level.GetString()));
        Assert.True(body.TryGetProperty("routingReason", out _));
    }

    [Fact]
    public async Task RoutingPreview_DayToDay_DoesNotDefaultToWorkplace()
    {
        await AuthAsAdmin();
        var request = new
        {
            cefrLevelOverride = "A2",
            learningGoals = new[] { "day_to_day" },
            allowReviewOrScaffold = false,
        };

        var response = await _client.PostAsJsonAsync("/api/admin/curriculum/routing-preview", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tags = body.GetProperty("contextTags").EnumerateArray().Select(e => e.GetString()).ToList();

        // workplace must not be the only / default context for day-to-day goals
        Assert.DoesNotContain(tags, t => t == "workplace" && tags.Count == 1);
    }

    // Phase I2C: RoutingPreview_DoesNotCreateReadinessItems removed — the readiness pool
    // (StudentActivityReadinessItem) it asserted was untouched no longer exists. See
    // docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.

    // ── Seeder: seed-only-missing strategy ───────────────────────────────────

    [Fact]
    public async Task Seeder_DoesNotOverwriteAdminEditedObjective()
    {
        await AuthAsAdmin();
        // Update a seeded objective via admin API
        var key = "a1.speaking.greetings_introductions";
        var request = new
        {
            key,
            title = "Admin Edited Title",
            description = "Admin edited this",
            cefrLevel = "A1",
            primarySkill = "speaking",
            secondarySkills = new[] { "vocabulary" },
            contextTags = new[] { "general_english" },
            focusTags = Array.Empty<string>(),
            prerequisiteObjectiveKeys = Array.Empty<string>(),
            recommendedOrder = 10,
            difficultyBand = 1,
            isActive = true,
            isReviewable = true,
            isExamInspired = false,
            teachingNotes = (string?)null,
            examplePrompts = (string?)null,
        };
        await _client.PutAsJsonAsync($"/api/admin/curriculum/objectives/{key}", request);

        // Run seeder again — should not touch existing key
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        await CurriculumObjectiveSeeder.SeedAsync(
            db, NullLogger.Instance);

        var obj = db.CurriculumObjectives.First(o => o.Key == key);
        Assert.Equal("Admin Edited Title", obj.Title);
    }
}
