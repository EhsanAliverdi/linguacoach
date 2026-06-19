using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for the T12 admin management endpoints.
/// Uses the base ApiTestFactory — no AI provider needed for these endpoints.
/// </summary>
public sealed class AdminManagementEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminManagementEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/admin/students ───────────────────────────────────────────────

    [Fact]
    public async Task ListStudents_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/admin/students")).StatusCode);
    }

    [Fact]
    public async Task ListStudents_AsAdmin_Returns200()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/students");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.True(body.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    public async Task ListStudents_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student403_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/students");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStudent_AsAdmin_UpdatesProfileFields()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"edit_{Guid.NewGuid():N}@t.com");
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.PutAsJsonAsync($"/api/admin/students/{studentProfileId}", new
        {
            firstName = "Mina",
            lastName = "Rahimi",
            displayName = "Mina R.",
            careerContext = "Project coordination",
            learningGoal = "Speak clearly in standups",
            learningGoalDescription = "Improve meeting updates",
            difficultSituationsText = "Interrupting politely",
            preferredSessionDurationMinutes = 30,
            professionalExperienceLevel = 3,
            roleFamiliarity = 2
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Mina", body.GetProperty("firstName").GetString());
        Assert.Equal("Project coordination", body.GetProperty("careerContext").GetString());
        Assert.Equal(30, body.GetProperty("preferredSessionDurationMinutes").GetInt32());
    }

    [Fact]
    public async Task UpdateStudent_AsStudent_Returns403()
    {
        var (studentToken, userId) = await _factory.CreateStudentAndGetTokenAsync($"edit403_{Guid.NewGuid():N}@t.com");
        var studentProfileId = await GetStudentProfileIdAsync(userId);

        var response = await ClientWithToken(studentToken).PutAsJsonAsync($"/api/admin/students/{studentProfileId}", new
        {
            firstName = "Not allowed"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveStudent_AsAdmin_HidesFromDefaultList_AndRejectsLogin()
    {
        var email = $"archive_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var archive = await client.PostAsync($"/api/admin/students/{studentProfileId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        var archivedBody = await archive.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Archived", archivedBody.GetProperty("lifecycleStage").GetString());

        var activeList = await client.GetFromJsonAsync<JsonElement>("/api/admin/students?pageSize=100");
        var activeItems = activeList.GetProperty("items");
        Assert.DoesNotContain(activeItems.EnumerateArray(), s => s.GetProperty("studentProfileId").GetString() == studentProfileId.ToString());

        var archivedList = await client.GetFromJsonAsync<JsonElement>("/api/admin/students?includeArchived=true&pageSize=100");
        var archivedItems = archivedList.GetProperty("items");
        Assert.Contains(archivedItems.EnumerateArray(), s => s.GetProperty("studentProfileId").GetString() == studentProfileId.ToString());

        var loginResponse = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Student@1234"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ArchiveStudent_AsStudent_Returns403()
    {
        var (studentToken, userId) = await _factory.CreateStudentAndGetTokenAsync($"archive403_{Guid.NewGuid():N}@t.com");
        var studentProfileId = await GetStudentProfileIdAsync(userId);

        var response = await ClientWithToken(studentToken).PostAsync($"/api/admin/students/{studentProfileId}/archive", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GET /api/admin/prompts ────────────────────────────────────────────────

    [Fact]
    public async Task ListPrompts_AsAdmin_Returns200WithItems()
    {
        await SeedPromptAsync();
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/prompts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(items.GetArrayLength() > 0);
    }

    [Fact]
    public async Task CreatePromptVersion_AsAdmin_Returns201()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/admin/prompts", new
        {
            key = $"test.prompt.{Guid.NewGuid():N}",
            content = "Test prompt content {{variable}}",
            maxInputTokens = 500,
            maxOutputTokens = 200
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.True(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CreatePromptVersion_SameKey_IncrementsVersion()
    {
        var key = $"versioned.{Guid.NewGuid():N}";
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync("/api/admin/prompts",
            new { key, content = "v1", maxInputTokens = 500, maxOutputTokens = 200 });
        var response = await client.PostAsJsonAsync("/api/admin/prompts",
            new { key, content = "v2", maxInputTokens = 500, maxOutputTokens = 200 });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task DeactivatePrompt_SetsIsActiveFalse()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var created = await client.PostAsJsonAsync("/api/admin/prompts", new
        {
            key = $"deact.{Guid.NewGuid():N}",
            content = "content",
            maxInputTokens = 400,
            maxOutputTokens = 100
        });
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var promptId = body.GetProperty("id").GetString();

        var deactivate = await client.PostAsync($"/api/admin/prompts/{promptId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var prompt = db.AiPrompts.AsNoTracking().First(p => p.Id == Guid.Parse(promptId!));
        Assert.False(prompt.IsActive);
    }

    [Fact]
    public async Task ActivatePrompt_SetsIsActiveTrue()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        // Create and immediately deactivate via direct DB to start in inactive state.
        var created = await client.PostAsJsonAsync("/api/admin/prompts", new
        {
            key = $"act.{Guid.NewGuid():N}",
            content = "content",
            maxInputTokens = 400,
            maxOutputTokens = 100
        });
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var promptId = body.GetProperty("id").GetString();
        var promptGuid = Guid.Parse(promptId!);

        // Deactivate first via HTTP
        await client.PostAsync($"/api/admin/prompts/{promptId}/deactivate", null);

        // Verify deactivated
        using (var s1 = _factory.Services.CreateScope())
        {
            var d1 = s1.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            Assert.False(d1.AiPrompts.AsNoTracking().First(p => p.Id == promptGuid).IsActive);
        }

        // Now activate
        var activate = await client.PostAsync($"/api/admin/prompts/{promptId}/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);

        using var s2 = _factory.Services.CreateScope();
        var d2 = s2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(d2.AiPrompts.AsNoTracking().First(p => p.Id == promptGuid).IsActive);
    }

    // ── GET /api/admin/careers + words ───────────────────────────────────────

    [Fact]
    public async Task ListCareers_AsAdmin_Returns200()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/careers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(items.GetArrayLength() > 0);
    }

    [Fact]
    public async Task AddCurriculumWord_NewWord_AppearsInWordList()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var career = db.CareerProfiles.First();
        var pair = db.LanguagePairs.First();

        var addResponse = await client.PostAsJsonAsync($"/api/admin/careers/{career.Id}/words", new
        {
            languagePairId = pair.Id,
            word = $"testword_{Guid.NewGuid():N}"[..20],
            definition = "A test word",
            exampleSentence = "Use the test word here.",
            priority = 99,
            tags = "test"
        });

        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var word = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A test word", word.GetProperty("definition").GetString());
    }

    [Fact]
    public async Task UpdateCurriculumWord_ChangesDefinition()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var career = db.CareerProfiles.First();
        var pair = db.LanguagePairs.First();

        var added = await client.PostAsJsonAsync($"/api/admin/careers/{career.Id}/words", new
        {
            languagePairId = pair.Id,
            word = $"upd_{Guid.NewGuid():N}"[..15],
            definition = "Original",
            exampleSentence = "Example.",
            priority = 50,
            tags = ""
        });
        var addedWord = await added.Content.ReadFromJsonAsync<JsonElement>();
        var wordId = addedWord.GetProperty("id").GetString();

        var updateResponse = await client.PutAsJsonAsync($"/api/admin/careers/words/{wordId}", new
        {
            definition = "Updated definition",
            exampleSentence = "New example.",
            priority = 51,
            tags = "updated"
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated definition", updated.GetProperty("definition").GetString());
    }

    // AI category configuration

    [Fact]
    public async Task ListAiCategories_Returns200()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/ai/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(items.GetArrayLength() >= 6);
    }

    [Fact]
    public async Task UpdateAiCategory_ChangesModelName()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.PatchAsJsonAsync("/api/admin/ai/categories/llm.default", new
        {
            providerName = "openai",
            modelName = "gpt-4o-mini"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("gpt-4o-mini", body.GetProperty("modelName").GetString());
    }

    [Fact]
    public async Task UpdateAiCategory_WithUnknownProvider_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.PatchAsJsonAsync("/api/admin/ai/categories/llm.default", new
        {
            providerName = "unknown-provider",
            modelName = "gpt-4o-mini"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListAiProviders_ReturnsProviderCatalog()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/ai-providers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(items.GetArrayLength() >= 3);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SeedPromptAsync()
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        if (!db.AiPrompts.Any())
        {
            db.AiPrompts.Add(new LinguaCoach.Domain.Entities.AiPrompt(
                "test.prompt.v1", "Test content", maxInputTokens: 500, maxOutputTokens: 200));
            await db.SaveChangesAsync();
        }
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetStudentProfileIdAsync(Guid userId)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return await db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstAsync();
    }
}
