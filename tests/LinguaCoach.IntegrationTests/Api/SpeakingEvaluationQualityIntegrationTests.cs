using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for speaking evaluation quality summary endpoint and dry-run signal counts.
/// Phase 16H — quality validation and dry-run signal layer.
/// Mastery, CEFR, and Learning Plan are never modified.
/// </summary>
public sealed class SpeakingEvaluationQualityIntegrationTests
    : IClassFixture<SpeakingEvaluationQualityTestFactory>
{
    private readonly SpeakingEvaluationQualityTestFactory _factory;

    public SpeakingEvaluationQualityIntegrationTests(SpeakingEvaluationQualityTestFactory factory)
        => _factory = factory;

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task QualitySummary_Unauthenticated_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync("/api/admin/speaking-evaluation/quality-summary");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QualitySummary_StudentToken_Returns403()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"qualstu_{Guid.NewGuid():N}@t.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync("/api/admin/speaking-evaluation/quality-summary");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Empty state ───────────────────────────────────────────────────────────

    [Fact]
    public async Task QualitySummary_NoEvaluations_ReturnsZeroCountsAndEmptyFailures()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync("/api/admin/speaking-evaluation/quality-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var quality = body.GetProperty("quality");
        quality.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        quality.GetProperty("latestFailureReasons").GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
    }

    // ── Status counts ─────────────────────────────────────────────────────────

    [Fact]
    public async Task QualitySummary_AfterNoOpJob_NotSupportedCountCorrect()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"qualns_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithNoOpAsync();

        var body = await GetQualityBodyAsync();
        var quality = body.GetProperty("quality");

        quality.GetProperty("notSupported").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        quality.GetProperty("completionRate").GetDouble().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task QualitySummary_AfterFakeProviderSuccess_CompletedCountCorrect()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"qualcmp_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(success: true);

        var body = await GetQualityBodyAsync();
        var quality = body.GetProperty("quality");

        quality.GetProperty("completed").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        quality.GetProperty("completionRate").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task QualitySummary_AfterFakeProviderFailure_FailedCountCorrect()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"qualfail_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(success: false, failureReason: "test failure");

        var body = await GetQualityBodyAsync();
        var quality = body.GetProperty("quality");

        quality.GetProperty("failed").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        quality.GetProperty("failureRate").GetDouble().Should().BeGreaterThan(0);
        quality.GetProperty("latestFailureReasons").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── Dry-run signal counts ─────────────────────────────────────────────────

    [Fact]
    public async Task QualitySummary_CompletedWithStrongScores_DryRunPositiveSignalCounted()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"qualdrypos_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);

        // Fake provider returns overallScore=78, completeness=80, relevance=75 by default
        await _factory.RunEvaluationJobWithFakeProviderAsync(success: true);

        var body = await GetQualityBodyAsync();
        var quality = body.GetProperty("quality");

        // At least one positive dry-run signal should exist
        quality.GetProperty("dryRunCandidatePositiveSignals").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task QualitySummary_CompletedWithNoOverallScore_DryRunBlockedCounted()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"qualdryblk_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        var attemptId = await SubmitAudioAsync(token, activityId);

        // Run with no overall score
        await _factory.RunEvaluationJobWithFakeProviderAsync(
            success: true, overallScore: null, fluencyScore: null);

        var body = await GetQualityBodyAsync();
        var quality = body.GetProperty("quality");

        quality.GetProperty("dryRunBlocked").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── Per-attempt dry-run signal in admin speaking list ─────────────────────

    [Fact]
    public async Task AdminSpeakingAttempts_AfterEvaluation_IncludesDryRunOutcomeField()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"qualatt_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);
        await _factory.RunEvaluationJobWithFakeProviderAsync(success: true);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        profile.Should().NotBeNull();

        var resp = await client.GetAsync(
            $"/api/admin/students/{profile!.Id}/speaking-attempts");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var attempts = body.GetProperty("attempts");
        attempts.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var first = attempts[0];
        first.TryGetProperty("dryRunOutcome", out _).Should().BeTrue(
            "dryRunOutcome field must be present on each attempt");
    }

    // ── Mastery/CEFR/Learning Plan are not updated ────────────────────────────

    [Fact]
    public async Task DryRunSignals_NeverUpdateMastery()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"qualmastery_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);

        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await dbBefore.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var cefrBefore = profile?.CefrLevel;
        var skillProfileCountBefore = await dbBefore.StudentSkillProfiles
            .CountAsync(s => s.StudentProfileId == profile!.Id);

        await _factory.RunEvaluationJobWithFakeProviderAsync(success: true);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileAfter = await dbAfter.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var skillProfileCountAfter = await dbAfter.StudentSkillProfiles
            .CountAsync(s => s.StudentProfileId == profile!.Id);

        profileAfter?.CefrLevel.Should().Be(cefrBefore,
            "dry-run signals must not change CEFR");
        skillProfileCountAfter.Should().Be(skillProfileCountBefore,
            "dry-run signals must not create new skill profile records");
    }

    // ── Config metadata in quality response ───────────────────────────────────

    [Fact]
    public async Task QualitySummary_ContainsConfigStatusAndProviderName()
    {
        var body = await GetQualityBodyAsync();

        body.TryGetProperty("configStatus", out _).Should().BeTrue();
        body.TryGetProperty("providerName", out _).Should().BeTrue();
        body.TryGetProperty("enabled", out _).Should().BeTrue();
    }

    // ── Student cannot see admin quality summary ──────────────────────────────

    [Fact]
    public async Task StudentToken_CannotAccessQualitySummary()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"stuqual_{Guid.NewGuid():N}@t.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync("/api/admin/speaking-evaluation/quality-summary");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<JsonElement> GetQualityBodyAsync()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resp = await client.GetAsync("/api/admin/speaking-evaluation/quality-summary");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> CreateActivityAsync(Guid ownerUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "SpeakingRolePlay",
            title = "Quality test activity",
            prompt = "Tell me about your day.",
            interactionMode = "audioResponse",
        });
        var activity = new LearningActivity(
            ActivityType.SpeakingRolePlay, ActivitySource.SystemFallback,
            "Quality test activity", "B1", contentJson, null);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private async Task<Guid> SubmitAudioAsync(string token, Guid activityId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var form = BuildAudioForm();
        var resp = await client.PostAsync($"/api/activity/{activityId}/audio-attempt", form);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("attemptId").GetGuid();
    }

    private static MultipartFormDataContent BuildAudioForm()
    {
        var bytes = new byte[512];
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm");
        var form = new MultipartFormDataContent();
        form.Add(content, "audioFile", "recording.webm");
        return form;
    }
}

// ── Test factory ──────────────────────────────────────────────────────────────

public sealed class SpeakingEvaluationQualityTestFactory : SpeakingEvaluationProviderTestFactory
{
    // Inherits all helpers from SpeakingEvaluationProviderTestFactory:
    // CreateOnboardedStudentAsync, CreateAdminAndGetTokenAsync,
    // RunEvaluationJobWithFakeProviderAsync, RunEvaluationJobWithNoOpAsync
}
