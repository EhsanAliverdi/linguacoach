using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Tests for GET /api/activity/{id}/audio-url:
///   - ownership check (other student is forbidden / not found)
///   - response includes url + expiresAt
///   - AudioAsset is preferred; legacy JSON StorageKey is the fallback
/// </summary>
public sealed class SignedUrlEndpointTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public SignedUrlEndpointTests(ActivityTestFactory factory) => _factory = factory;

    [Fact]
    public async Task AudioUrl_LegacyJsonKeyFallback_ReturnsUrlAndExpiry()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"signed_legacy_{Guid.NewGuid():N}@test.com");
        var activityId = await CreateListeningActivity(userId, generateAudio: true, withAudioAsset: false);

        var resp = await ClientWithToken(token).GetAsync($"/api/activity/{activityId}/audio-url");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("url").GetString()));
        var expiresAt = body.GetProperty("expiresAt").GetString();
        Assert.False(string.IsNullOrWhiteSpace(expiresAt));
        Assert.True(DateTimeOffset.TryParse(expiresAt, out var parsed));
        Assert.True(parsed > DateTimeOffset.UtcNow);
        // Fake storage signals streaming-endpoint fallback.
        Assert.Equal($"/api/activity/{activityId}/audio", body.GetProperty("url").GetString());
    }

    [Fact]
    public async Task AudioUrl_PrefersAudioAsset()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"signed_asset_{Guid.NewGuid():N}@test.com");
        var activityId = await CreateListeningActivity(userId, generateAudio: true, withAudioAsset: true);

        var resp = await ClientWithToken(token).GetAsync($"/api/activity/{activityId}/audio-url");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("expiresAt", out _));
        // No raw object key is ever leaked to the client.
        Assert.DoesNotContain("audio-assets-key", body.GetRawText());
    }

    [Fact]
    public async Task AudioUrl_OtherStudent_IsForbidden()
    {
        var (_, ownerUserId) = await _factory.CreateOnboardedStudentAsync($"signed_owner_{Guid.NewGuid():N}@test.com");
        var (otherToken, _) = await _factory.CreateOnboardedStudentAsync($"signed_other_{Guid.NewGuid():N}@test.com");
        var activityId = await CreateListeningActivity(ownerUserId, generateAudio: true, withAudioAsset: false, attachToOwnedModule: true);

        var resp = await ClientWithToken(otherToken).GetAsync($"/api/activity/{activityId}/audio-url");

        Assert.True(resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AudioUrl_RequiresAuth()
    {
        var (_, userId) = await _factory.CreateOnboardedStudentAsync($"signed_auth_{Guid.NewGuid():N}@test.com");
        var activityId = await CreateListeningActivity(userId, generateAudio: true, withAudioAsset: false);

        var resp = await _factory.CreateClient().GetAsync($"/api/activity/{activityId}/audio-url");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private async Task<Guid> CreateListeningActivity(
        Guid userId, bool generateAudio, bool withAudioAsset, bool attachToOwnedModule = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        Guid? moduleId = null;
        if (attachToOwnedModule)
        {
            var path = new LearningPath(profile.Id, "Signed url path", "ctx");
            db.LearningPaths.Add(path);
            await db.SaveChangesAsync();
            var module = new LearningModule(path.Id, "Signed url module", "desc", 1);
            db.LearningModules.Add(module);
            await db.SaveChangesAsync();
            moduleId = module.Id;
        }

        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "ListeningComprehension",
            title = "Audio url activity",
            scenario = "scenario",
            instructions = "instructions",
            audioScript = "Please review the latest schedule before the meeting.",
            transcriptAvailableAfterSubmit = true,
            questions = new[] { new { id = "q1", question = "What?", expectedAnswer = "schedule", type = "short_answer" } }
        });

        var activity = new LearningActivity(
            ActivityType.ListeningComprehension, ActivitySource.AiGenerated,
            "Audio url activity", "B1", contentJson, learningModuleId: moduleId);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        if (generateAudio)
        {
            var audio = scope.ServiceProvider.GetRequiredService<LinguaCoach.Infrastructure.Activity.ListeningAudioService>();
            await audio.EnsureAudioAsync(activity, "en", CancellationToken.None);
            await db.SaveChangesAsync();
        }

        if (withAudioAsset)
        {
            db.AudioAssets.Add(new AudioAsset(
                profile.Id, AssetType.ListeningTts, "tts-audio/from-asset.wav", "audio/wav",
                learningActivityId: activity.Id, generationStatus: GenerationStatus.Ready));
            await db.SaveChangesAsync();
        }

        return activity.Id;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
