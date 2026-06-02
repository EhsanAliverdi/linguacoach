using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Full integration tests for the AI provider config endpoints:
///   GET  /api/admin/ai-config/providers  — catalog
///   GET  /api/admin/ai-config            — feature configs
///   PUT  /api/admin/ai-config/{id}       — update provider + model
///   PUT  /api/admin/ai-config/{id}/api-key — store / clear API key
/// All tests use SQLite in-memory via ApiTestFactory; no real AI provider called.
/// </summary>
public sealed class AiConfigEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AiConfigEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/admin/ai-config/providers ─────────────────────────────────────

    [Fact]
    public async Task ListProviders_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/ai-config/providers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListProviders_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"prov403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-config/providers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListProviders_AsAdmin_ReturnsAllThreeProviders()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-config/providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var providers = body.EnumerateArray().Select(p => p.GetProperty("providerName").GetString()).ToList();
        Assert.Contains("openai", providers);
        Assert.Contains("gemini", providers);
        Assert.Contains("anthropic", providers);
    }

    [Fact]
    public async Task ListProviders_GeminiEntry_IncludesNewFlashModels()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-config/providers");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var gemini = body.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("providerName").GetString() == "gemini");
        Assert.NotEqual(default, gemini);

        var models = gemini.GetProperty("models").EnumerateArray()
            .Select(m => m.GetString()).ToList();

        Assert.Contains("gemini-2.5-flash", models);
        Assert.Contains("gemini-2.5-pro", models);
        Assert.Contains("gemini-2.5-flash-lite", models);
    }

    [Fact]
    public async Task ListProviders_AnthropicEntry_IncludesClaudeModels()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-config/providers");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var anthropic = body.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("providerName").GetString() == "anthropic");

        var models = anthropic.GetProperty("models").EnumerateArray()
            .Select(m => m.GetString()).ToList();

        Assert.Contains("claude-sonnet-4-6", models);
        Assert.Contains("claude-opus-4-8", models);
    }

    // ── GET /api/admin/ai-config ───────────────────────────────────────────────

    [Fact]
    public async Task ListConfigs_ReturnsHasStoredApiKeyFalse_WhenNoKeyStored()
    {
        var configId = await SeedConfigAsync("writing.exercise");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.EnumerateArray().First(i => i.GetProperty("id").GetString() == configId.ToString());
        Assert.False(item.GetProperty("hasStoredApiKey").GetBoolean());
    }

    // ── PUT /api/admin/ai-config/{id} ─────────────────────────────────────────

    [Fact]
    public async Task UpdateConfig_ToGemini_Succeeds()
    {
        var configId = await SeedConfigAsync($"gemini.feature.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}",
            new { providerName = "gemini", modelName = "gemini-2.5-flash" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("gemini", body.GetProperty("providerName").GetString());
        Assert.Equal("gemini-2.5-flash", body.GetProperty("modelName").GetString());
    }

    [Fact]
    public async Task UpdateConfig_ToAnthropic_Succeeds()
    {
        var configId = await SeedConfigAsync($"anthropic.feature.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}",
            new { providerName = "anthropic", modelName = "claude-sonnet-4-6" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("anthropic", body.GetProperty("providerName").GetString());
        Assert.Equal("claude-sonnet-4-6", body.GetProperty("modelName").GetString());
    }

    [Fact]
    public async Task UpdateConfig_ToGemini25Pro_Succeeds()
    {
        var configId = await SeedConfigAsync($"gemini25.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}",
            new { providerName = "gemini", modelName = "gemini-2.5-pro" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfig_UnknownProvider_Returns400()
    {
        var configId = await SeedConfigAsync($"unk.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}",
            new { providerName = "cohere", modelName = "command-r" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Unsupported", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UpdateConfig_WrongModelForProvider_Returns400()
    {
        var configId = await SeedConfigAsync($"badmodel.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}",
            new { providerName = "anthropic", modelName = "gpt-4o" }); // OpenAI model, wrong provider

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfig_NonExistentId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{Guid.NewGuid()}",
            new { providerName = "openai", modelName = "gpt-4o-mini" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfig_PersistsToDatabase()
    {
        var configId = await SeedConfigAsync($"persist.model.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}",
            new { providerName = "gemini", modelName = "gemini-2.0-flash" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var config = await db.AiProviderConfigs.AsNoTracking().FirstAsync(c => c.Id == configId);
        Assert.Equal("gemini", config.ProviderName);
        Assert.Equal("gemini-2.0-flash", config.ModelName);
    }

    // ── PUT /api/admin/ai-config/{id}/api-key ─────────────────────────────────

    [Fact]
    public async Task UpdateApiKey_StoresKey_AndReturnsHasStoredApiKeyTrue()
    {
        var configId = await SeedConfigAsync($"apikey.store.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}/api-key",
            new { apiKey = "sk-test-fake-key-12345" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("hasStoredApiKey").GetBoolean());
    }

    [Fact]
    public async Task UpdateApiKey_DoesNotExposeKeyInResponse()
    {
        var configId = await SeedConfigAsync($"apikey.noexpose.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}/api-key",
            new { apiKey = "sk-secret-key-should-not-appear" });

        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sk-secret-key-should-not-appear", rawJson);
    }

    [Fact]
    public async Task UpdateApiKey_StoredInDatabase()
    {
        var configId = await SeedConfigAsync($"apikey.db.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}/api-key",
            new { apiKey = "sk-db-persisted-key" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var config = await db.AiProviderConfigs.AsNoTracking().FirstAsync(c => c.Id == configId);
        Assert.Equal("sk-db-persisted-key", config.ApiKey);
    }

    [Fact]
    public async Task UpdateApiKey_WithNull_ClearsStoredKey()
    {
        var configId = await SeedConfigAsync($"apikey.clear.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        // Store a key first
        await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}/api-key",
            new { apiKey = "sk-to-be-cleared" });

        // Clear it
        var clearResponse = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}/api-key",
            new { apiKey = (string?)null });

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var body = await clearResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasStoredApiKey").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var config = await db.AiProviderConfigs.AsNoTracking().FirstAsync(c => c.Id == configId);
        Assert.Null(config.ApiKey);
    }

    [Fact]
    public async Task UpdateApiKey_WithEmptyString_ClearsStoredKey()
    {
        var configId = await SeedConfigAsync($"apikey.empty.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}/api-key",
            new { apiKey = "sk-some-key" });

        var clearResponse = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}/api-key",
            new { apiKey = "" });

        var body = await clearResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasStoredApiKey").GetBoolean());
    }

    [Fact]
    public async Task UpdateApiKey_NonExistentId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{Guid.NewGuid()}/api-key",
            new { apiKey = "sk-test" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateApiKey_AsStudent_Returns403()
    {
        var configId = await SeedConfigAsync($"apikey.403.{Guid.NewGuid():N}");
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student.ak.{Guid.NewGuid():N}@t.com");

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{configId}/api-key",
            new { apiKey = "sk-student-attempt" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> SeedConfigAsync(string featureKey)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var existing = db.AiProviderConfigs.FirstOrDefault(c => c.FeatureKey == featureKey);
        if (existing is not null) return existing.Id;

        var config = new LinguaCoach.Domain.Entities.AiProviderConfig(featureKey, "openai", "gpt-4o");
        db.AiProviderConfigs.Add(config);
        await db.SaveChangesAsync();
        return config.Id;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
