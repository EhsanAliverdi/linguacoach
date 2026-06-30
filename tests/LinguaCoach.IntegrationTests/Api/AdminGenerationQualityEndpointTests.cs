using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AdminGenerationQualityEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminGenerationQualityEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/api/admin/generation-quality/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Summary_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"gq403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/generation-quality/summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── response shape ────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_Returns200WithExpectedShape()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/generation-quality/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Required top-level fields
        Assert.True(body.TryGetProperty("recentDays", out _));
        Assert.True(body.TryGetProperty("validationFailureSummary", out var summary));
        Assert.True(summary.TryGetProperty("totalFailures", out _));
        Assert.True(summary.TryGetProperty("abandonedGenerations", out _));
        Assert.True(summary.TryGetProperty("failuresLast24Hours", out _));

        Assert.Equal(JsonValueKind.Array, body.GetProperty("latestFailures").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("patternFailureBreakdown").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("cefrFailureBreakdown").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("promptSummary").ValueKind);

        // Counts are non-negative integers
        Assert.True(summary.GetProperty("totalFailures").GetInt32() >= 0);
        Assert.True(summary.GetProperty("abandonedGenerations").GetInt32() >= 0);
        Assert.True(summary.GetProperty("failuresLast24Hours").GetInt32() >= 0);
    }

    // ── prompt summary ────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_PromptSummary_ContainsSeededActivePrompts()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/generation-quality/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var prompts = body.GetProperty("promptSummary").EnumerateArray().ToList();
        Assert.NotEmpty(prompts); // seeder creates prompts

        var first = prompts[0];
        Assert.True(first.TryGetProperty("key", out _));
        Assert.True(first.TryGetProperty("version", out _));
        Assert.True(first.TryGetProperty("isActive", out _));
        Assert.True(first.TryGetProperty("seededAtUtc", out _));
        // Secrets never returned — no content field at this endpoint
        Assert.False(first.TryGetProperty("content", out _));
    }

    // ── validation failure data ───────────────────────────────────────────────

    [Fact]
    public async Task Summary_WithStoredFailures_ReturnsCorrectCounts()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Seed two failures: one first-attempt, one retry (abandoned)
        db.GenerationValidationFailures.Add(new GenerationValidationFailure(
            activityTypeName: "WritingScenario",
            validationErrors: "Missing prompt field.",
            attemptNumber: 1,
            patternKey: "email_reply",
            cefrLevel: "B1"));

        db.GenerationValidationFailures.Add(new GenerationValidationFailure(
            activityTypeName: "ListeningComprehension",
            validationErrors: "Empty audioScript field.",
            attemptNumber: 2,
            patternKey: "listen_and_answer",
            cefrLevel: "A2"));

        await db.SaveChangesAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/generation-quality/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var summary = body.GetProperty("validationFailureSummary");
        Assert.True(summary.GetProperty("totalFailures").GetInt32() >= 2);
        Assert.True(summary.GetProperty("abandonedGenerations").GetInt32() >= 1);
    }

    [Fact]
    public async Task Summary_PatternBreakdown_GroupsCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var uniquePattern = $"test_pattern_{Guid.NewGuid():N}";
        db.GenerationValidationFailures.Add(new GenerationValidationFailure(
            activityTypeName: "WritingScenario",
            validationErrors: "Error A.",
            attemptNumber: 1,
            patternKey: uniquePattern,
            cefrLevel: "B1"));
        db.GenerationValidationFailures.Add(new GenerationValidationFailure(
            activityTypeName: "WritingScenario",
            validationErrors: "Error B.",
            attemptNumber: 2,
            patternKey: uniquePattern,
            cefrLevel: "B2"));

        await db.SaveChangesAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/generation-quality/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var breakdown = body.GetProperty("patternFailureBreakdown").EnumerateArray().ToList();
        var entry = breakdown.FirstOrDefault(p => p.GetProperty("patternKey").GetString() == uniquePattern);
        Assert.NotEqual(default, entry);
        Assert.True(entry.GetProperty("totalFailures").GetInt32() >= 2);
        Assert.True(entry.GetProperty("abandonedCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Summary_DoesNotExposeSecrets()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var rawJson = await ClientWithToken(token)
            .GetStringAsync("/api/admin/generation-quality/summary");

        // No provider secrets should appear
        Assert.DoesNotContain("apiKey", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretKey", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessKey", rawJson, StringComparison.OrdinalIgnoreCase);
        // No storage paths
        Assert.DoesNotContain("bucketName", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Summary_InvalidRecentDays_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/generation-quality/summary?recentDays=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        response = await ClientWithToken(token)
            .GetAsync("/api/admin/generation-quality/summary?recentDays=91");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
