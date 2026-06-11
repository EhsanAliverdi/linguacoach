using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for the redesigned AI config endpoints:
///
///   Feature routing:
///     GET  /api/admin/ai-config            — list feature configs
///     PUT  /api/admin/ai-config/{id}       — update provider + model for a feature
///
///   Provider credentials:
///     GET  /api/admin/ai-providers         — catalog with credential status
///     PUT  /api/admin/ai-providers/{p}/api-key — store / clear API key per provider
///     POST /api/admin/ai-providers/{p}/test    — verify connectivity (fake provider)
/// </summary>
public sealed class AiConfigEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AiConfigEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/admin/ai-providers (catalog) ─────────────────────────────────

    [Fact]
    public async Task ListProviders_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/ai-providers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListProviders_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"prov403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-providers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListProviders_AsAdmin_ReturnsAllThreeProviders()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var names = body.EnumerateArray().Select(p => p.GetProperty("providerName").GetString()).ToList();
        Assert.Contains("openai", names);
        Assert.Contains("gemini", names);
        Assert.Contains("anthropic", names);
    }

    [Fact]
    public async Task ListProviders_GeminiEntry_IncludesFlash25Models()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var body = await ClientWithToken(token).GetFromJsonAsync<JsonElement>("/api/admin/ai-providers");

        var gemini = body.EnumerateArray().First(p => p.GetProperty("providerName").GetString() == "gemini");
        var models = gemini.GetProperty("models").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("gemini-2.5-flash", models);
        Assert.Contains("gemini-2.5-pro", models);
        Assert.Contains("gemini-2.5-flash-lite", models);
    }

    [Fact]
    public async Task ListProviders_ResponseShapeHasRequiredFields()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var body = await ClientWithToken(token).GetFromJsonAsync<JsonElement>("/api/admin/ai-providers");

        foreach (var p in body.EnumerateArray())
        {
            Assert.True(p.TryGetProperty("providerName", out _));
            Assert.True(p.TryGetProperty("hasApiKey", out _));
            Assert.True(p.TryGetProperty("models", out _));
            Assert.True(p.TryGetProperty("modelTests", out _));
        }
    }

    // ── PUT /api/admin/ai-providers/{p}/api-key ───────────────────────────────

    [Fact]
    public async Task SetApiKey_Stores_AndHasApiKeyBecomesTrue()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/ai-providers/openai/api-key",
            new { apiKey = "sk-test-fake-openai-key" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("openai", body.GetProperty("providerName").GetString());
    }

    [Fact]
    public async Task SetApiKey_DoesNotExposeKeyInResponse()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/ai-providers/gemini/api-key",
            new { apiKey = "AIza-super-secret-should-not-appear" });

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("AIza-super-secret-should-not-appear", raw);
    }

    [Fact]
    public async Task SetApiKey_PersistedToDatabase()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/ai-providers/anthropic/api-key",
            new { apiKey = "sk-ant-db-persisted" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var cred = db.AiProviderCredentials.AsNoTracking().FirstOrDefault(c => c.ProviderName == "anthropic");
        Assert.NotNull(cred);
        Assert.Equal("sk-ant-db-persisted", cred.ApiKey);
    }

    [Fact]
    public async Task SetApiKey_WithNull_ClearsKey()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/ai-providers/openai/api-key",
            new { apiKey = "sk-to-clear" });

        var clear = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/ai-providers/openai/api-key",
            new { apiKey = (string?)null });

        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var body = await clear.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasApiKey").GetBoolean());
    }

    [Fact]
    public async Task SetApiKey_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"st.ak.{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/ai-providers/openai/api-key",
            new { apiKey = "sk-student-attempt" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── POST /api/admin/ai-providers/{p}/test ─────────────────────────────────

    [Fact]
    public async Task TestProvider_WithFakeProvider_ReturnsOk()
    {
        var factory = new AiTestWithFakeTesterFactory();
        await factory.InitializeAsync();
        var token = await factory.CreateAdminAndGetTokenAsync();

        var response = await factory.CreateClientWithToken(token)
            .PostAsync("/api/admin/ai-providers/openai/test", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Returns AiProviderCatalogItem — check modelTests array has entries
        Assert.Equal("openai", body.GetProperty("providerName").GetString());
        var modelTests = body.GetProperty("modelTests").EnumerateArray().ToList();
        Assert.NotEmpty(modelTests);
        // TTS-only models (tts-*, *-tts, cosyvoice-v2) are skipped during connection test
        // and remain at ok=false — only assert on chat-capable models.
        var chatModels = modelTests.Where(m =>
        {
            var name = m.GetProperty("modelName").GetString() ?? "";
            return !name.StartsWith("tts-", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("-tts", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("cosyvoice-v2", StringComparison.OrdinalIgnoreCase);
        }).ToList();
        Assert.NotEmpty(chatModels);
        Assert.All(chatModels, m => Assert.True(m.GetProperty("ok").GetBoolean()));

        await factory.DisposeAsync();
    }

    [Fact]
    public async Task TestProvider_RecordsResultInDatabase()
    {
        var factory = new AiTestWithFakeTesterFactory();
        await factory.InitializeAsync();
        var token = await factory.CreateAdminAndGetTokenAsync();

        await factory.CreateClientWithToken(token).PostAsync("/api/admin/ai-providers/gemini/test", null);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var cred = db.AiProviderCredentials.AsNoTracking().FirstOrDefault(c => c.ProviderName == "gemini");
        Assert.NotNull(cred);
        Assert.NotEmpty(cred.ModelTests);
        Assert.All(cred.ModelTests, kvp => Assert.True(kvp.Value.Ok));

        await factory.DisposeAsync();
    }

    [Fact]
    public async Task TestProvider_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"st.test.{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).PostAsync("/api/admin/ai-providers/openai/test", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GET /api/admin/ai-config (feature routing) ────────────────────────────

    [Fact]
    public async Task ListConfigs_AsAdmin_Returns200()
    {
        await SeedFeatureConfigAsync("list.test");
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ListConfigs_AsAdmin_IncludesActiveRuntimeFeatureKeysAndFallbackFields()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.EnumerateArray().ToList();
        var keys = items.Select(i => i.GetProperty("featureKey").GetString()).ToList();

        Assert.Contains("placement_assessment_evaluate", keys);
        Assert.Contains("activity_generate_speaking_roleplay", keys);
        Assert.Contains("activity_evaluate_speaking_roleplay", keys);
        Assert.Contains("activity_generate_listening", keys);
        Assert.Contains("vocabulary_extract_from_attempt", keys);
        Assert.Contains("student_memory_update", keys);

        var first = items.First();
        Assert.True(first.TryGetProperty("fallbackProviderName", out _));
        Assert.True(first.TryGetProperty("fallbackModelName", out _));
        Assert.True(first.TryGetProperty("fallbackEnabled", out _));
    }

    // ── PUT /api/admin/ai-config/{id} (feature routing) ──────────────────────

    [Fact]
    public async Task UpdateFeatureConfig_ToGemini25Flash_Succeeds()
    {
        var id = await SeedFeatureConfigAsync($"feat.gemini.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new { providerName = "gemini", modelName = "gemini-2.5-flash" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("gemini", body.GetProperty("providerName").GetString());
        Assert.Equal("gemini-2.5-flash", body.GetProperty("modelName").GetString());
    }

    [Fact]
    public async Task UpdateFeatureConfig_ToAnthropic_Succeeds()
    {
        var id = await SeedFeatureConfigAsync($"feat.ant.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new { providerName = "anthropic", modelName = "claude-sonnet-4-6" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFeatureConfig_FallbackProviderModelAndToggle_Succeeds()
    {
        var id = await SeedFeatureConfigAsync($"feat.fallback.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new
            {
                providerName = "openai",
                modelName = "gpt-4o-mini",
                fallbackProviderName = "gemini",
                fallbackModelName = "gemini-2.5-flash",
                fallbackEnabled = true
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("gemini", body.GetProperty("fallbackProviderName").GetString());
        Assert.Equal("gemini-2.5-flash", body.GetProperty("fallbackModelName").GetString());
        Assert.True(body.GetProperty("fallbackEnabled").GetBoolean());
    }

    [Fact]
    public async Task UpdateFeatureConfig_InvalidFallbackModel_Returns400()
    {
        var id = await SeedFeatureConfigAsync($"feat.badfallback.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new
            {
                fallbackProviderName = "gemini",
                fallbackModelName = "gpt-4o",
                fallbackEnabled = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFeatureConfig_UnknownProvider_Returns400()
    {
        var id = await SeedFeatureConfigAsync($"feat.unk.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new { providerName = "cohere", modelName = "command-r" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFeatureConfig_WrongModelForProvider_Returns400()
    {
        var id = await SeedFeatureConfigAsync($"feat.bad.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new { providerName = "anthropic", modelName = "gpt-4o" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFeatureConfig_NonExistentId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{Guid.NewGuid()}",
            new { providerName = "openai", modelName = "gpt-4o-mini" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> SeedFeatureConfigAsync(string featureKey)
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
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }
}

/// <summary>
/// Factory that replaces IAiProviderTester with a fast fake that always returns OK.
/// Used for test-provider endpoint tests so no real network call is made.
/// </summary>
public sealed class AiTestWithFakeTesterFactory : ApiTestFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var existing = services.SingleOrDefault(d => d.ServiceType == typeof(IAiProviderTester));
            if (existing is not null) services.Remove(existing);
            services.AddSingleton<IAiProviderTester, FakeAiProviderTester>();
        });
    }

    public HttpClient CreateClientWithToken(string token)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }
}

internal sealed class FakeAiProviderTester : IAiProviderTester
{
    public Task<IReadOnlyList<ModelTestOutcome>> TestAllModelsAsync(
        string providerName,
        IEnumerable<string> models,
        string? apiKeyOverride,
        CancellationToken ct = default)
    {
        IReadOnlyList<ModelTestOutcome> results = models
            .Select(m => new ModelTestOutcome(m, true, 42, null))
            .ToList();
        return Task.FromResult(results);
    }
}
