using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 17B writing evaluation quality summary and dry-run endpoints.
/// Verifies auth requirements, admin-only access, and correct aggregate counts.
/// Dry-run signals are never applied to mastery, CEFR, or Learning Plan.
/// </summary>
public sealed class WritingEvaluationQualitySummaryTests
    : IClassFixture<WritingEvaluationEnabledTestFactory>
{
    private readonly WritingEvaluationEnabledTestFactory _factory;

    public WritingEvaluationQualitySummaryTests(WritingEvaluationEnabledTestFactory factory)
    {
        _factory = factory;
    }

    // ── Test 16: GET quality-summary requires admin (401 for anonymous) ────────

    [Fact]
    public async Task GetQualitySummary_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync("/api/admin/writing-evaluation/quality-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Test 17: Student cannot access admin quality summary (403) ────────────

    [Fact]
    public async Task GetQualitySummary_Student_Returns403()
    {
        var (studentToken, _) = await _factory.CreateOnboardedStudentAsync(
            $"wq_student_{Guid.NewGuid():N}@t.com");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", studentToken);

        var resp = await client.GetAsync("/api/admin/writing-evaluation/quality-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Test 18: Quality summary returns correct counts for seeded data ────────

    [Fact]
    public async Task GetQualitySummary_WithSeededEvaluations_ReturnsCorrectCounts()
    {
        var (studentToken, _) = await _factory.CreateOnboardedStudentAsync(
            $"wq_counts_{Guid.NewGuid():N}@t.com");

        var activityId = await CreateWritingActivityAsync(_factory);
        await SubmitWritingAsync(_factory, studentToken, activityId);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync("/api/admin/writing-evaluation/quality-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalEvaluations").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("note").GetString().Should().Contain("Dry-run only");
        body.GetProperty("note").GetString().Should().Contain("not applied to mastery");
    }

    // ── Dry-run endpoint: non-existent evaluation returns 404 ─────────────────

    [Fact]
    public async Task GetWithDryRun_NonExistentId_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync($"/api/admin/writing-evaluation/{Guid.NewGuid()}/dry-run");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Dry-run endpoint: existing evaluation returns signal ──────────────────

    [Fact]
    public async Task GetWithDryRun_ExistingEvaluation_ReturnsDryRunSignal()
    {
        var (studentToken, _) = await _factory.CreateOnboardedStudentAsync(
            $"wq_dryrun_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateWritingActivityAsync(_factory);
        await SubmitWritingAsync(_factory, studentToken, activityId);

        Guid evaluationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            evaluationId = await db.WritingEvaluations
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => e.Id)
                .FirstAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync($"/api/admin/writing-evaluation/{evaluationId}/dry-run");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("evaluationId").GetGuid().Should().Be(evaluationId);
        body.GetProperty("dryRunSignal").ValueKind.Should().NotBe(JsonValueKind.Null);

        var outcome = body.GetProperty("dryRunSignal").GetProperty("outcome").GetString();
        outcome.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetWithDryRun_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync($"/api/admin/writing-evaluation/{Guid.NewGuid()}/dry-run");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<Guid> CreateWritingActivityAsync(ActivityTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "WritingScenario",
            title = "Quality summary test activity",
            scenario = "Write a short follow-up email.",
            prompt = "Write a professional follow-up email.",
        });

        var activity = new LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.SystemFallback,
            title: "Quality test writing activity",
            difficulty: "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: null);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private static async Task<Guid> SubmitWritingAsync(ActivityTestFactory factory, string token, Guid activityId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Dear Team, I wanted to follow up on our discussion. Best regards." });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("attemptId").GetGuid();
    }
}
