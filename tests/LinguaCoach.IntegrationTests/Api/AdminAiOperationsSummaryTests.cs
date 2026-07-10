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
/// Phase 20A — read-only admin AI operations dashboard summary endpoint.
/// Verifies: auth guards, safe empty-database response, correct operational counts
/// once speaking/writing evaluations exist, signal-gate state visibility,
/// and that no raw prompt/provider payload/secret text ever leaks into the response.
/// Phase I2C: the readinessPoolAiSummary section was removed along with the readiness pool —
/// see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed class AdminAiOperationsSummaryTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminAiOperationsSummaryTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/ai-operations/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Summary_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"aiops403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai-operations/summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Admin can load; safe shape ───────────────────────────────────────────

    [Fact]
    public async Task Summary_AsAdmin_ReturnsOkWithExpectedShape()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/ai-operations/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.True(body.TryGetProperty("overallStatus", out _));
        Assert.True(body.TryGetProperty("providerUsage", out _));
        Assert.True(body.TryGetProperty("speakingEvaluationSummary", out _));
        Assert.True(body.TryGetProperty("writingEvaluationSummary", out _));
        Assert.True(body.TryGetProperty("generationQualitySummary", out _));
        Assert.True(body.TryGetProperty("signalGateSummary", out _));
        Assert.True(body.TryGetProperty("recentFailures", out _));
        Assert.True(body.TryGetProperty("unavailableSections", out _));
    }

    // ── Safe empty-database response ─────────────────────────────────────────

    [Fact]
    public async Task Summary_EmptyDatabase_ReturnsSafeZeroSummary_NotError()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/ai-operations/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Zero counts, not an error, and no exception payload.
        var speaking = body.GetProperty("speakingEvaluationSummary");
        Assert.True(speaking.GetProperty("pendingCount").GetInt32() >= 0);
        Assert.True(speaking.GetProperty("completedCount").GetInt32() >= 0);
    }

    // ── Speaking / writing evaluation counts ─────────────────────────────────

    [Fact]
    public async Task Summary_IncludesSpeakingEvaluationCounts()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"aiopsspk_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

            var failed = SpeakingEvaluation.CreatePending(Guid.NewGuid(), profile.Id, Guid.NewGuid());
            failed.MarkEvaluating("FakeProvider", "fake-model");
            failed.MarkFailed("Provider timeout while evaluating audio.");
            db.SpeakingEvaluations.Add(failed);

            var completed = SpeakingEvaluation.CreatePending(Guid.NewGuid(), profile.Id, Guid.NewGuid());
            completed.MarkEvaluating("FakeProvider", "fake-model");
            completed.MarkCompleted("transcript", 80, 80, 80, 80, 80, "good", "keep practicing");
            db.SpeakingEvaluations.Add(completed);

            await db.SaveChangesAsync();
        }

        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/ai-operations/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var speaking = body.GetProperty("speakingEvaluationSummary");
        Assert.True(speaking.GetProperty("failedCount").GetInt32() >= 1);
        Assert.True(speaking.GetProperty("completedCount").GetInt32() >= 1);

        var recentFailures = body.GetProperty("recentFailures").EnumerateArray().ToList();
        Assert.Contains(recentFailures, f => f.GetProperty("area").GetString() == "Speaking");
    }

    [Fact]
    public async Task Summary_IncludesWritingEvaluationCounts()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"aiopswrt_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

            var failed = WritingEvaluation.CreatePending(Guid.NewGuid(), profile.Id, Guid.NewGuid());
            failed.MarkEvaluating("FakeProvider", "fake-model");
            failed.MarkFailed("Provider returned malformed JSON.");
            db.WritingEvaluations.Add(failed);

            await db.SaveChangesAsync();
        }

        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/ai-operations/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var writing = body.GetProperty("writingEvaluationSummary");
        Assert.True(writing.GetProperty("failedCount").GetInt32() >= 1);

        var recentFailures = body.GetProperty("recentFailures").EnumerateArray().ToList();
        Assert.Contains(recentFailures, f => f.GetProperty("area").GetString() == "Writing");
    }

    // Phase I2C: Summary_IncludesReviewScaffoldAndPilotConfigState removed — the
    // readinessPoolAiSummary section it verified was deleted along with the readiness pool. See
    // docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.

    // ── Signal gate state ─────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_IncludesSignalGateState_AllMutationGatesDisabledByDefault()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/ai-operations/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var gates = body.GetProperty("signalGateSummary");
        Assert.False(gates.GetProperty("speakingCefrUpdatesEnabled").GetBoolean());
        Assert.False(gates.GetProperty("writingCefrUpdatesEnabled").GetBoolean());
        Assert.False(gates.GetProperty("speakingObjectiveCompletionEnabled").GetBoolean());
        Assert.False(gates.GetProperty("writingObjectiveCompletionEnabled").GetBoolean());
        Assert.False(gates.GetProperty("speakingLearningPlanAutoRegenEnabled").GetBoolean());
        Assert.False(gates.GetProperty("writingLearningPlanAutoRegenEnabled").GetBoolean());
        Assert.False(gates.GetProperty("anyInvariantViolationsDetected").GetBoolean());
    }

    // ── No sensitive payload leakage ──────────────────────────────────────────

    [Fact]
    public async Task Summary_DoesNotExposeRawPromptOrProviderSecrets()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"aiopssafe_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

            var failed = SpeakingEvaluation.CreatePending(Guid.NewGuid(), profile.Id, Guid.NewGuid());
            failed.MarkEvaluating("FakeProvider", "fake-model");
            failed.MarkFailed("Provider timeout while evaluating audio.");
            db.SpeakingEvaluations.Add(failed);
            await db.SaveChangesAsync();
        }

        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/ai-operations/summary");
        var raw = await response.Content.ReadAsStringAsync();
        var lower = raw.ToLowerInvariant();

        Assert.DoesNotContain("apikey", lower);
        Assert.DoesNotContain("api_key", lower);
        Assert.DoesNotContain("bearer ", lower);
        Assert.DoesNotContain("secret", lower);
        Assert.DoesNotContain("sk-", lower);
        Assert.DoesNotContain("system prompt", lower);
        Assert.DoesNotContain("\"transcript\"", lower);
        Assert.DoesNotContain("\"prompt\"", lower);
    }
}
