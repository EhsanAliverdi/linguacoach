using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class TtsConfigEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public TtsConfigEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task CategoryList_IncludesTtsListeningAndPlacement()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var body = await ClientWithToken(token).GetFromJsonAsync<JsonElement>("/api/admin/ai/categories");

        var keys = body.EnumerateArray()
            .Select(c => c.GetProperty("categoryKey").GetString())
            .ToHashSet();

        Assert.Contains("tts.listening", keys);
        Assert.Contains("tts.placement", keys);
    }

    [Fact]
    public async Task TtsCategories_HaveProviderModelAndVoiceFields()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var body = await ClientWithToken(token).GetFromJsonAsync<JsonElement>("/api/admin/ai/categories");

        foreach (var categoryKey in new[] { "tts.listening", "tts.placement" })
        {
            var config = body.EnumerateArray()
                .FirstOrDefault(c => c.GetProperty("categoryKey").GetString() == categoryKey);

            Assert.NotEqual(default, config);
            Assert.True(config.TryGetProperty("providerName", out _));
            Assert.True(config.TryGetProperty("modelName", out _));
            Assert.True(config.TryGetProperty("voiceName", out _));
        }
    }

    [Fact]
    public async Task UpdateTtsCategory_SetVoiceName_Persists()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PatchAsJsonAsync(
            "/api/admin/ai/categories/tts.listening",
            new { providerName = "openai", modelName = "tts-1", voiceName = "nova" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("nova", body.GetProperty("voiceName").GetString());
    }

    [Fact]
    public async Task UpdateTtsCategory_SetVoiceNameToNull_ClearsVoice()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(token).PatchAsJsonAsync(
            "/api/admin/ai/categories/tts.placement",
            new { providerName = "openai", modelName = "tts-1", voiceName = "onyx" });

        var response = await ClientWithToken(token).PatchAsJsonAsync(
            "/api/admin/ai/categories/tts.placement",
            new { providerName = "openai", modelName = "tts-1", voiceName = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("voiceName").ValueKind);
    }

    [Fact]
    public async Task UpdateTtsCategory_SwitchToOpenAiTts1Hd_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PatchAsJsonAsync(
            "/api/admin/ai/categories/tts.placement",
            new { providerName = "openai", modelName = "tts-1-hd", voiceName = "onyx" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("openai", body.GetProperty("providerName").GetString());
        Assert.Equal("tts-1-hd", body.GetProperty("modelName").GetString());
    }

    [Fact]
    public async Task TtsCategorySeeder_IsIdempotent_NoDuplicateRows()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var listeningCount = await db.AiConfigCategories.CountAsync(c => c.CategoryKey == "tts.listening");
        var placementCount = await db.AiConfigCategories.CountAsync(c => c.CategoryKey == "tts.placement");

        Assert.Equal(1, listeningCount);
        Assert.Equal(1, placementCount);
    }

    private HttpClient ClientWithToken(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }
}
