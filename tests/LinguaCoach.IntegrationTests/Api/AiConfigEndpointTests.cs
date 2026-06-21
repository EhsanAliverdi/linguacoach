using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AiConfigEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AiConfigEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

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
    public async Task ListProviders_AsAdmin_ReturnsConfiguredProviders()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var names = body.EnumerateArray().Select(p => p.GetProperty("providerName").GetString()).ToList();
        Assert.Contains("openai", names);
        Assert.Contains("gemini", names);
        Assert.Contains("anthropic", names);
        Assert.Contains("qwen", names);
    }

    [Fact]
    public async Task SetApiKey_StoresKeyAndDoesNotExposeIt()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/ai-providers/gemini/api-key",
            new { apiKey = "AIza-super-secret-should-not-appear" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("AIza-super-secret-should-not-appear", raw);
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.True(body.GetProperty("hasApiKey").GetBoolean());
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
    public async Task TestProvider_RecordsResultsForChatModels()
    {
        var factory = new AiTestWithFakeTesterFactory();
        await factory.InitializeAsync();
        var token = await factory.CreateAdminAndGetTokenAsync();

        var response = await factory.CreateClientWithToken(token)
            .PostAsync("/api/admin/ai-providers/openai/test", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var chatModels = body.GetProperty("modelTests").EnumerateArray()
            .Where(m => !IsTtsOnlyModel(m.GetProperty("modelName").GetString() ?? ""))
            .ToList();
        Assert.NotEmpty(chatModels);
        Assert.All(chatModels, m => Assert.True(m.GetProperty("ok").GetBoolean()));

        await factory.DisposeAsync();
    }

    [Fact]
    public async Task AddProviderModel_AddsModelToCatalog()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var modelName = $"gpt-custom-{Guid.NewGuid():N}";

        var response = await ClientWithToken(token).PostAsJsonAsync(
            "/api/admin/ai-providers/openai/models",
            new { modelName });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var models = body.GetProperty("models").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains(modelName, models);
    }

    [Fact]
    public async Task TestProviderModel_RecordsSingleModelResult()
    {
        var factory = new AiTestWithFakeTesterFactory();
        await factory.InitializeAsync();
        var token = await factory.CreateAdminAndGetTokenAsync();
        var modelName = $"gemini-custom-{Guid.NewGuid():N}";

        var response = await factory.CreateClientWithToken(token).PostAsJsonAsync(
            "/api/admin/ai-providers/gemini/models/test",
            new { modelName });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var test = body.GetProperty("modelTests").EnumerateArray()
            .First(m => m.GetProperty("modelName").GetString() == modelName);
        Assert.True(test.GetProperty("ok").GetBoolean());

        await factory.DisposeAsync();
    }

    [Fact]
    public async Task LegacyFeatureConfigEndpoint_IsRemoved()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-config");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListCategories_AsAdmin_ReturnsSixCategoryCards()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var keys = body.EnumerateArray().Select(i => i.GetProperty("categoryKey").GetString()).ToList();

        Assert.Contains("llm.default", keys);
        Assert.Contains("llm.generation", keys);
        Assert.Contains("llm.evaluation", keys);
        Assert.Contains("llm.memory", keys);
        Assert.Contains("tts.listening", keys);
        Assert.Contains("tts.placement", keys);
    }

    [Fact]
    public async Task UpdateCategory_ToGemini25Flash_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PatchAsJsonAsync(
            "/api/admin/ai/categories/llm.default",
            new { providerName = "gemini", modelName = "gemini-2.5-flash" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("gemini", body.GetProperty("providerName").GetString());
        Assert.Equal("gemini-2.5-flash", body.GetProperty("modelName").GetString());
    }

    [Fact]
    public async Task UpdateTtsCategory_WithVoice_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PatchAsJsonAsync(
            "/api/admin/ai/categories/tts.listening",
            new { providerName = "openai", modelName = "tts-1", voiceName = "onyx" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("openai", body.GetProperty("providerName").GetString());
        Assert.Equal("tts-1", body.GetProperty("modelName").GetString());
        Assert.Equal("onyx", body.GetProperty("voiceName").GetString());
    }

    [Fact]
    public async Task TestCategory_AsAdmin_ReturnsResult()
    {
        var factory = new AiTestWithFakeTesterFactory();
        await factory.InitializeAsync();
        var token = await factory.CreateAdminAndGetTokenAsync();
        await factory.CreateClientWithToken(token).PutAsJsonAsync(
            "/api/admin/ai-providers/openai/api-key",
            new { apiKey = "sk-test" });
        await factory.CreateClientWithToken(token).PatchAsJsonAsync(
            "/api/admin/ai/categories/llm.default",
            new { providerName = "openai", modelName = "gpt-4o-mini" });

        var response = await factory.CreateClientWithToken(token)
            .PostAsync("/api/admin/ai/categories/llm.default/test", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("ok").GetBoolean());

        await factory.DisposeAsync();
    }

    private HttpClient ClientWithToken(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static bool IsTtsOnlyModel(string modelName)
    {
        var lower = modelName.ToLowerInvariant();
        return lower.StartsWith("tts-") || lower.Contains("-tts") || lower == "cosyvoice-v2";
    }

    // ── Pricing endpoint ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListAiPricing_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/ai/pricing");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListAiPricing_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"pricing403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai/pricing");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListAiPricing_AsAdmin_ReturnsConfiguredRows()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai/pricing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
        var list = rows.EnumerateArray().ToList();
        Assert.NotEmpty(list);

        var providers = list.Select(r => r.GetProperty("providerName").GetString()).ToList();
        Assert.Contains("openai", providers);
        Assert.Contains("gemini", providers);
        Assert.Contains("anthropic", providers);
    }

    [Fact]
    public async Task ListAiPricing_AsAdmin_RowsHaveRequiredFields()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai/pricing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        foreach (var row in rows)
        {
            Assert.True(row.TryGetProperty("providerName", out _), "missing providerName");
            Assert.True(row.TryGetProperty("modelName", out _), "missing modelName");
            Assert.True(row.TryGetProperty("inputPer1KTokens", out _), "missing inputPer1KTokens");
            Assert.True(row.TryGetProperty("outputPer1KTokens", out _), "missing outputPer1KTokens");
            Assert.True(row.TryGetProperty("currency", out var currency), "missing currency");
            Assert.Equal("USD", currency.GetString());
            Assert.True(row.TryGetProperty("source", out var source), "missing source");
            Assert.Equal("Configuration", source.GetString());
            Assert.True(row.TryGetProperty("isConfigured", out var configured), "missing isConfigured");
            Assert.True(configured.GetBoolean());
        }
    }

    [Fact]
    public async Task ListAiPricing_AsAdmin_OpenAiGpt4oPriceMatchesConfig()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai/pricing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        var gpt4o = rows.FirstOrDefault(r =>
            r.GetProperty("providerName").GetString() == "openai" &&
            r.GetProperty("modelName").GetString() == "gpt-4o");

        Assert.True(gpt4o.ValueKind != JsonValueKind.Undefined, "gpt-4o pricing row not found");
        Assert.Equal(0.0025m, gpt4o.GetProperty("inputPer1KTokens").GetDecimal());
        Assert.Equal(0.01m, gpt4o.GetProperty("outputPer1KTokens").GetDecimal());
    }
}

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
