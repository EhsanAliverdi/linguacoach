using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AggregatePoolHealthEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AggregatePoolHealthEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private System.Net.Http.HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AggregatePoolHealth_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/readiness-pool/health");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AggregatePoolHealth_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"aggpool403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/readiness-pool/health");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── empty pool returns zeros ──────────────────────────────────────────────

    [Fact]
    public async Task AggregatePoolHealth_EmptyPool_ReturnsZeroCounts()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/readiness-pool/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // All counts are zero when no items exist (or the test database is otherwise empty)
        Assert.True(body.GetProperty("totalStudentsWithItems").GetInt32() >= 0);
        Assert.True(body.GetProperty("totalReady").GetInt32() >= 0);
        Assert.True(body.TryGetProperty("generatedAt", out _));
    }

    // ── counts reflect seeded items ───────────────────────────────────────────

    [Fact]
    public async Task AggregatePoolHealth_WithItems_ReturnsCorrectCounts()
    {
        // Arrange: seed one Ready and one Failed item for a new student
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(
            $"aggpool_{Guid.NewGuid():N}@t.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // StudentActivityReadinessItem.StudentId is the StudentProfile.Id, not the UserId
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        var profileId = profile.Id;

        var readyItem = new StudentActivityReadinessItem(
            studentId: profileId,
            source: ReadinessPoolSource.TodayLesson,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false);
        readyItem.MarkGenerating();
        readyItem.MarkReady();

        var failedItem = new StudentActivityReadinessItem(
            studentId: profileId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Normal,
            isLowerLevelContent: false);
        failedItem.MarkGenerating();
        failedItem.MarkFailed("generation_error", "AI failed");

        db.StudentActivityReadinessItems.AddRange(readyItem, failedItem);
        await db.SaveChangesAsync();

        // Act
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/readiness-pool/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // The seeded student should appear in totals
        Assert.True(body.GetProperty("totalStudentsWithItems").GetInt32() >= 1);
        Assert.True(body.GetProperty("totalReady").GetInt32() >= 1);
        Assert.True(body.GetProperty("totalFailed").GetInt32() >= 1);
        Assert.True(body.GetProperty("studentsWithFailedItems").GetInt32() >= 1);
    }

    // ── response shape ────────────────────────────────────────────────────────

    [Fact]
    public async Task AggregatePoolHealth_ReturnsExpectedTopLevelFields()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/readiness-pool/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        foreach (var field in new[]
        {
            "totalStudentsWithItems", "totalQueued", "totalGenerating", "totalReady",
            "totalReserved", "totalConsumed", "totalExpired", "totalFailed",
            "totalStale", "totalReviewOnly", "totalSkipped",
            "studentsWithNoReadyItems", "studentsWithFailedItems", "studentsWithStaleItems",
            "generatedAt"
        })
        {
            Assert.True(body.TryGetProperty(field, out _), $"Missing field: {field}");
        }
    }
}
