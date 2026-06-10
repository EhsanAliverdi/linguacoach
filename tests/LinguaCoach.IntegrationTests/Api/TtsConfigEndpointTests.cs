using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for TTS provider config: seeding, voice field, and admin endpoints.
/// </summary>
public sealed class TtsConfigEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public TtsConfigEndpointTests(ApiTestFactory factory) => _factory = factory;

    // ── DefaultAiSeeder seeds tts.* configs ───────────────────────────────────

    [Fact]
    public async Task AiConfigList_IncludesTtsListeningAndPlacementKeys()
    {
        // The seeder runs on startup via ApiTestFactory.InitializeAsync.
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var body = await ClientWithToken(token).GetFromJsonAsync<JsonElement>("/api/admin/ai-config");

        var keys = body.EnumerateArray()
            .Select(c => c.GetProperty("featureKey").GetString())
            .ToHashSet();

        Assert.Contains("tts.listening", keys);
        Assert.Contains("tts.placement", keys);
    }

    [Fact]
    public async Task TtsConfigs_DefaultToFakeProvider()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var body = await ClientWithToken(token).GetFromJsonAsync<JsonElement>("/api/admin/ai-config");

        foreach (var featureKey in new[] { "tts.listening", "tts.placement" })
        {
            var config = body.EnumerateArray()
                .FirstOrDefault(c => c.GetProperty("featureKey").GetString() == featureKey);

            Assert.NotEqual(default, config);
            Assert.Equal("fake", config.GetProperty("providerName").GetString());
            Assert.Equal("fake", config.GetProperty("modelName").GetString());
        }
    }

    // ── VoiceName field in the response ──────────────────────────────────────

    [Fact]
    public async Task AiConfigItem_HasVoiceNameField()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var body = await ClientWithToken(token).GetFromJsonAsync<JsonElement>("/api/admin/ai-config");

        // Every item in the list should have a voiceName property.
        foreach (var item in body.EnumerateArray())
        {
            Assert.True(item.TryGetProperty("voiceName", out _),
                $"Missing voiceName on featureKey={item.GetProperty("featureKey").GetString()}");
        }
    }

    // ── Admin can set voice name via PUT ──────────────────────────────────────

    [Fact]
    public async Task UpdateTtsConfig_SetVoiceName_Persists()
    {
        var id = await SeedTtsConfigAsync("tts.listening.voice.test");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new { voiceName = "nova" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("nova", body.GetProperty("voiceName").GetString());
    }

    [Fact]
    public async Task UpdateTtsConfig_SetVoiceNameToNull_ClearsVoice()
    {
        var id = await SeedTtsConfigAsync("tts.placement.voice.clear.test", voiceName: "onyx");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new { voiceName = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("voiceName").ValueKind);
    }

    // ── Admin can switch TTS provider to openai with tts-1 model ─────────────

    [Fact]
    public async Task UpdateTtsConfig_SwitchToOpenAiTts1_Succeeds()
    {
        var id = await SeedTtsConfigAsync($"tts.listening.openai.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new { providerName = "openai", modelName = "tts-1", voiceName = "onyx" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("openai", body.GetProperty("providerName").GetString());
        Assert.Equal("tts-1", body.GetProperty("modelName").GetString());
        Assert.Equal("onyx", body.GetProperty("voiceName").GetString());
    }

    [Fact]
    public async Task UpdateTtsConfig_SwitchToOpenAiTts1Hd_Succeeds()
    {
        var id = await SeedTtsConfigAsync($"tts.placement.hd.{Guid.NewGuid():N}");
        var token = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai-config/{id}",
            new { providerName = "openai", modelName = "tts-1-hd" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("tts-1-hd", body.GetProperty("modelName").GetString());
    }

    // ── Seeder is idempotent ──────────────────────────────────────────────────

    [Fact]
    public async Task TtsSeeder_IsIdempotent_NoDuplicateRows()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var listeningCount = await db.AiProviderConfigs.CountAsync(c => c.FeatureKey == "tts.listening");
        var placementCount = await db.AiProviderConfigs.CountAsync(c => c.FeatureKey == "tts.placement");

        Assert.Equal(1, listeningCount);
        Assert.Equal(1, placementCount);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> SeedTtsConfigAsync(string featureKey, string? voiceName = null)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var existing = db.AiProviderConfigs.FirstOrDefault(c => c.FeatureKey == featureKey);
        if (existing is not null) return existing.Id;
        var config = new AiProviderConfig(featureKey, "fake", "fake", voiceName);
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
