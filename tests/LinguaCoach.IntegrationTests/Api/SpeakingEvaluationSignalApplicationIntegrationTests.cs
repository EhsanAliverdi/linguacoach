using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 16I — speaking evaluation mastery signal application.
/// Covers: config gating, applied-signal admin endpoint, idempotency, mastery/CEFR invariants.
/// Uses fake provider — no live AI calls.
/// </summary>
public sealed class SpeakingEvaluationSignalApplicationIntegrationTests
    : IClassFixture<SpeakingEvaluationSignalApplicationTestFactory>
{
    private readonly SpeakingEvaluationSignalApplicationTestFactory _factory;

    public SpeakingEvaluationSignalApplicationIntegrationTests(
        SpeakingEvaluationSignalApplicationTestFactory factory)
        => _factory = factory;

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AppliedSignals_Unauthenticated_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync("/api/admin/speaking-evaluation/applied-signals");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AppliedSignals_StudentToken_Returns403()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"sigstu_{Guid.NewGuid():N}@t.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync("/api/admin/speaking-evaluation/applied-signals");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Default config: ApplyMasterySignals=false ─────────────────────────────

    [Fact]
    public async Task AppliedSignals_DefaultConfig_ShowsIntegrationDisabled()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync("/api/admin/speaking-evaluation/applied-signals");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("masteryIntegrationEnabled").GetBoolean().Should().BeFalse();
        body.GetProperty("objectiveCompletionAllowed").GetBoolean().Should().BeFalse();
        body.GetProperty("cefrUpdateAllowed").GetBoolean().Should().BeFalse();
    }

    // ── Config-disabled prevents signal application ───────────────────────────

    [Fact]
    public async Task ConfigDisabled_CompletedEvaluation_NoSignalApplied()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"sigoff_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);
        await _factory.RunEvaluationJobWithFakeProviderAsync(
            success: true, overallScore: 75, fluencyScore: 75);

        await _factory.RunSignalApplicationAsync(applyMasterySignals: false);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var evalId = await db.SpeakingEvaluations
            .Where(e => e.LearningActivityId == activityId)
            .Select(e => e.Id).FirstOrDefaultAsync();
        var applied = await db.SpeakingEvaluationAppliedSignals
            .AnyAsync(s => s.EvaluationId == evalId);
        applied.Should().BeFalse("config is disabled");
    }

    // ── Config-enabled applies review signal ─────────────────────────────────

    [Fact]
    public async Task ConfigEnabled_HighConfidenceReviewSignal_IsApplied()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"sigrev_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);
        // overallScore=55 (review range), all dims present + feedback = High confidence
        await _factory.RunEvaluationJobWithFakeProviderAsync(
            success: true, overallScore: 55, fluencyScore: 55);

        await _factory.RunSignalApplicationAsync(applyMasterySignals: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var evalId = await db.SpeakingEvaluations
            .Where(e => e.LearningActivityId == activityId)
            .Select(e => e.Id).FirstOrDefaultAsync();
        var applied = await db.SpeakingEvaluationAppliedSignals
            .FirstOrDefaultAsync(s => s.EvaluationId == evalId);
        applied.Should().NotBeNull();
        applied!.SignalType.Should().Be("Review");
    }

    // ── Applied signal is idempotent ──────────────────────────────────────────

    [Fact]
    public async Task SignalApplication_IsIdempotent_SecondRunDoesNotDuplicate()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"sigdup_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);
        await _factory.RunEvaluationJobWithFakeProviderAsync(
            success: true, overallScore: 55, fluencyScore: 55);

        await _factory.RunSignalApplicationAsync(applyMasterySignals: true);
        await _factory.RunSignalApplicationAsync(applyMasterySignals: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var evalId = await db.SpeakingEvaluations
            .Where(e => e.LearningActivityId == activityId)
            .Select(e => e.Id).FirstOrDefaultAsync();
        var count = await db.SpeakingEvaluationAppliedSignals
            .CountAsync(s => s.EvaluationId == evalId);
        count.Should().Be(1, "idempotent — second run skips already-applied evaluation");
    }

    // ── Failed evaluation does not produce applied signal ────────────────────

    [Fact]
    public async Task FailedEvaluation_DoesNotProduceAppliedSignal()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"sigfail_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);
        await _factory.RunEvaluationJobWithFakeProviderAsync(success: false, failureReason: "Provider error.");

        await _factory.RunSignalApplicationAsync(applyMasterySignals: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var evalId = await db.SpeakingEvaluations
            .Where(e => e.LearningActivityId == activityId)
            .Select(e => e.Id).FirstOrDefaultAsync();
        var applied = await db.SpeakingEvaluationAppliedSignals
            .AnyAsync(s => s.EvaluationId == evalId);
        applied.Should().BeFalse("failed evaluations must not affect mastery");
    }

    // ── Admin endpoint shows applied counts ──────────────────────────────────

    [Fact]
    public async Task AdminEndpoint_AfterAppliedSignal_ShowsCorrectCounts()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"sigcnt_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);
        await _factory.RunEvaluationJobWithFakeProviderAsync(
            success: true, overallScore: 55, fluencyScore: 55);
        await _factory.RunSignalApplicationAsync(applyMasterySignals: true);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resp = await client.GetAsync("/api/admin/speaking-evaluation/applied-signals");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("appliedSignals").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── CEFR is not changed ───────────────────────────────────────────────────

    [Fact]
    public async Task AfterSignalApplication_CefrIsNotChanged()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"sigcefr_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);

        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var cefrBefore = await dbBefore.StudentProfiles
            .Where(p => p.Id == userId)
            .Select(p => p.CefrLevel)
            .FirstOrDefaultAsync();

        await _factory.RunEvaluationJobWithFakeProviderAsync(
            success: true, overallScore: 80, fluencyScore: 80);
        await _factory.RunSignalApplicationAsync(applyMasterySignals: true, allowPositiveSignals: true);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var cefrAfter = await dbAfter.StudentProfiles
            .Where(p => p.Id == userId)
            .Select(p => p.CefrLevel)
            .FirstOrDefaultAsync();

        cefrAfter.Should().Be(cefrBefore, "CEFR must never be changed by speaking AI in Phase 16I");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateActivityAsync(Guid _)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var contentJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            activityType = "SpeakingRolePlay",
            title = "Signal application test activity",
            prompt = "Describe your role.",
            interactionMode = "audioResponse",
        });
        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            LinguaCoach.Domain.Enums.ActivityType.SpeakingRolePlay,
            LinguaCoach.Domain.Enums.ActivitySource.SystemFallback,
            "Signal application test activity", "B1", contentJson);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private async Task SubmitAudioAsync(string token, Guid activityId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var form = BuildAudioForm();
        await client.PostAsync($"/api/activity/{activityId}/audio-attempt", form);
    }

    private static MultipartFormDataContent BuildAudioForm()
    {
        var bytes = new byte[512];
        var content = new System.Net.Http.ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm");
        var form = new MultipartFormDataContent();
        form.Add(content, "audioFile", "recording.webm");
        return form;
    }
}

// ── Test factory ──────────────────────────────────────────────────────────────

public sealed class SpeakingEvaluationSignalApplicationTestFactory
    : SpeakingEvaluationProviderTestFactory
{
    public async Task RunSignalApplicationAsync(
        bool applyMasterySignals = false,
        bool allowReviewSignals = true,
        bool allowPositiveSignals = false,
        string minimumConfidence = "High")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var opts = Options.Create(new SpeakingEvaluationOptions
        {
            ApplyMasterySignals = applyMasterySignals,
            AllowReviewSignals = allowReviewSignals,
            AllowPositiveSignals = allowPositiveSignals,
            MinimumConfidenceForMasterySignal = minimumConfidence,
        });
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILogger<SpeakingEvaluationSignalApplicationService>>();
        var svc = new SpeakingEvaluationSignalApplicationService(db, opts, logger);
        await svc.ApplyPendingSignalsAsync(20);
    }
}
