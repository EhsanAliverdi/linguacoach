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
/// Integration tests for provider-backed speaking evaluation pipeline.
/// Phase 16G — fake provider used throughout; no live AI calls in normal test suite.
/// </summary>
public sealed class SpeakingEvaluationProviderIntegrationTests
    : IClassFixture<SpeakingEvaluationProviderTestFactory>
{
    private readonly SpeakingEvaluationProviderTestFactory _factory;

    public SpeakingEvaluationProviderIntegrationTests(SpeakingEvaluationProviderTestFactory factory)
        => _factory = factory;

    // ── Provider unsupported → NotSupported ───────────────────────────────────

    [Fact]
    public async Task NoOpProvider_EvaluationRecord_BecomesNotSupported()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"noop_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithNoOpAsync();

        // After NoOp job run, all pending records become NotSupported
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var unsupported = await db.SpeakingEvaluations
            .Where(e => e.Status == SpeakingEvaluationStatus.NotSupported)
            .AnyAsync();
        unsupported.Should().BeTrue();
    }

    // ── Fake provider success: Pending → Completed ────────────────────────────

    [Fact]
    public async Task FakeProvider_Success_EvaluationBecomesCompleted()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"fakeprov_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        var attemptId = await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(success: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eval = await db.SpeakingEvaluations
            .FirstOrDefaultAsync(e => e.ActivityAttemptId == attemptId);

        eval.Should().NotBeNull();
        eval!.Status.Should().Be(SpeakingEvaluationStatus.Completed);
        eval.FeedbackText.Should().NotBeNullOrWhiteSpace();
        eval.SuggestedImprovement.Should().NotBeNullOrWhiteSpace();
        eval.ProviderName.Should().Be("fake");
        eval.PronunciationScore.Should().BeNull("fake provider does not claim pronunciation scoring");
    }

    // ── Fake provider failure: Pending → Failed ───────────────────────────────

    [Fact]
    public async Task FakeProvider_Failure_EvaluationBecomesFailed()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"fakefail_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        var attemptId = await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(success: false, failureReason: "simulated failure");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eval = await db.SpeakingEvaluations
            .FirstOrDefaultAsync(e => e.ActivityAttemptId == attemptId);

        eval.Should().NotBeNull();
        eval!.Status.Should().Be(SpeakingEvaluationStatus.Failed);
        eval.FailureReason.Should().NotBeNullOrWhiteSpace();
        eval.RetryCount.Should().Be(1);
    }

    // ── Partial response: nullable fields persisted correctly ─────────────────

    [Fact]
    public async Task FakeProvider_PartialScores_NullableFieldsPersistedCorrectly()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"partial_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        var attemptId = await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(
            success: true, overallScore: 70, fluencyScore: null, transcript: null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eval = await db.SpeakingEvaluations
            .FirstOrDefaultAsync(e => e.ActivityAttemptId == attemptId);

        eval.Should().NotBeNull();
        eval!.Status.Should().Be(SpeakingEvaluationStatus.Completed);
        eval.OverallScore.Should().Be(70);
        eval.FluencyScore.Should().BeNull();
        eval.Transcript.Should().BeNull();
    }

    // ── Retry: Failed evaluation re-processed on next job run ─────────────────

    [Fact]
    public async Task FakeProvider_FailedEvaluation_IsRetriedByJob()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"retry_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        var attemptId = await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(success: false, failureReason: "first failure");
        await _factory.RunEvaluationJobWithFakeProviderAsync(success: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eval = await db.SpeakingEvaluations
            .FirstOrDefaultAsync(e => e.ActivityAttemptId == attemptId);

        eval!.Status.Should().Be(SpeakingEvaluationStatus.Completed);
    }

    // ── Completed evaluation is not retried ───────────────────────────────────

    [Fact]
    public async Task CompletedEvaluation_NotRetried_OnSubsequentJobRun()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"nodup_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        var attemptId = await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(success: true);
        // Second run with a failing provider — should not touch the Completed record
        await _factory.RunEvaluationJobWithFakeProviderAsync(success: false, failureReason: "should not reach");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eval = await db.SpeakingEvaluations
            .FirstOrDefaultAsync(e => e.ActivityAttemptId == attemptId);

        eval!.Status.Should().Be(SpeakingEvaluationStatus.Completed);
    }

    // ── Job continues after provider throw ────────────────────────────────────

    [Fact]
    public async Task Job_ContinuesAfterProviderThrow_DoesNotCrash()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"throw_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        await SubmitAudioAsync(token, activityId);

        // Should not throw — job catches provider errors per-evaluation
        var ex = await Record.ExceptionAsync(() =>
            _factory.RunEvaluationJobWithThrowingProviderAsync());

        ex.Should().BeNull("the job must swallow per-evaluation provider errors");
    }

    // ── Admin status endpoint ─────────────────────────────────────────────────

    [Fact]
    public async Task AdminStatus_Unauthenticated_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync("/api/admin/speaking-evaluation/status");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AdminStatus_StudentToken_Returns403()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"stuauth_{Guid.NewGuid():N}@t.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync("/api/admin/speaking-evaluation/status");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AdminStatus_Admin_ReturnsValidStatusDto()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync("/api/admin/speaking-evaluation/status");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("configStatus").GetString()
            .Should().BeOneOf("Disabled", "NoOp", "ProviderConfigured", "ProviderUnsupported", "Enabled");
        body.TryGetProperty("providerName", out _).Should().BeTrue();
        body.TryGetProperty("enabled", out _).Should().BeTrue();
        body.TryGetProperty("capabilities", out _).Should().BeTrue();
    }

    // ── GET evaluation: completed state shows feedback ─────────────────────────

    [Fact]
    public async Task GetEvaluation_AfterFakeProviderCompletes_ReturnsCompletedDto()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"geteval_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        var attemptId = await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(success: true);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync(
            $"/api/activity/{activityId}/attempts/{attemptId}/evaluation");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Completed");
        body.GetProperty("feedbackText").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ── No fake scores returned when provider omits them ─────────────────────

    [Fact]
    public async Task GetEvaluation_NoScores_ReturnsNullScoreFields()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"noscore_{Guid.NewGuid():N}@t.com");
        var activityId = await CreateActivityAsync(userId);
        var attemptId = await SubmitAudioAsync(token, activityId);

        await _factory.RunEvaluationJobWithFakeProviderAsync(
            success: true, overallScore: null, fluencyScore: null);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.GetAsync(
            $"/api/activity/{activityId}/attempts/{attemptId}/evaluation");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Completed");
        body.GetProperty("overallScore").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("pronunciationScore").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateActivityAsync(Guid ownerUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "SpeakingRolePlay",
            title = "Provider integration test activity",
            prompt = "Describe your workplace in 30 seconds.",
            interactionMode = "audioResponse",
        });
        var activity = new LearningActivity(
            ActivityType.SpeakingRolePlay, ActivitySource.SystemFallback,
            "Provider integration test activity", "B1", contentJson, null);
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
        var bytes = new byte[1024];
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        var form = new MultipartFormDataContent();
        form.Add(content, "audioFile", "recording.webm");
        return form;
    }
}

// ── FakeSpeakingEvaluationProvider ───────────────────────────────────────────

/// <summary>
/// Configurable fake speaking evaluation provider for integration tests.
/// No network calls. Allows injection into SpeakingEvaluationService directly.
/// </summary>
internal sealed class FakeSpeakingEvaluationProvider : ISpeakingEvaluationProvider
{
    public string ProviderName => "fake";
    public bool IsSupported => true;
    public SpeakingEvaluationProviderCapabilities Capabilities =>
        SpeakingEvaluationProviderCapabilities.OpenAiWhisperGpt;

    private readonly bool _success;
    private readonly string? _failureReason;
    private readonly double? _overallScore;
    private readonly double? _fluencyScore;
    private readonly double? _completenessScore;
    private readonly double? _relevanceScore;
    private readonly string? _transcript;
    private readonly bool _throws;

    public FakeSpeakingEvaluationProvider(
        bool success = true,
        string? failureReason = null,
        double? overallScore = 78,
        double? fluencyScore = 72,
        double? completenessScore = 80,
        double? relevanceScore = 75,
        string? transcript = "The learner described their morning routine clearly.",
        bool throws = false)
    {
        _success = success;
        _failureReason = failureReason;
        _overallScore = overallScore;
        _fluencyScore = fluencyScore;
        _completenessScore = completenessScore;
        _relevanceScore = relevanceScore;
        _transcript = transcript;
        _throws = throws;
    }

    public Task<SpeakingEvaluationProviderResult> EvaluateAsync(
        SpeakingEvaluationRequest request, CancellationToken ct = default)
    {
        if (_throws) throw new InvalidOperationException("FakeSpeakingEvaluationProvider: simulated throw.");

        return Task.FromResult(new SpeakingEvaluationProviderResult(
            Success: _success,
            Transcript: _success ? _transcript : null,
            OverallScore: _success ? _overallScore : null,
            FluencyScore: _success ? _fluencyScore : null,
            PronunciationScore: null,
            CompletenessScore: _success ? _completenessScore : null,
            RelevanceScore: _success ? _relevanceScore : null,
            FeedbackText: _success
                ? "This feedback is AI-assisted and may be approximate. Good effort on your response."
                : null,
            SuggestedImprovement: _success ? "Try to add a specific example next time." : null,
            FailureReason: _success ? null : (_failureReason ?? "Fake provider failure."),
            ModelName: _success ? "fake-model" : null));
    }
}

// ── Test factory ──────────────────────────────────────────────────────────────

public class SpeakingEvaluationProviderTestFactory : ActivityTestFactory
{
    public override async Task<(string Token, Guid UserId)> CreateOnboardedStudentAsync(
        string email = "speaking_eval_student@test.linguacoach.com")
    {
        await SeedPromptTemplateAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        foreach (var key in new[] {
            "activity_generate_speaking_roleplay",
            "activity_evaluate_speaking_roleplay",
            "activity_generate_listening" })
        {
            if (!db.AiPrompts.Any(p => p.Key == key))
            {
                db.AiPrompts.Add(new AiPrompt(
                    key, "fake-prompt-{{cefrLevel}}", maxInputTokens: 800, maxOutputTokens: 1000));
                await db.SaveChangesAsync();
            }
        }
        return await base.CreateOnboardedStudentAsync(email);
    }

    private SpeakingEvaluationService BuildSvc(
        LinguaCoachDbContext db,
        ISpeakingEvaluationProvider provider,
        bool enabled = true)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SpeakingEvaluationOptions
        {
            Enabled = enabled,
            Provider = provider.ProviderName,
            MaxBatchSize = 20,
            MaxRetries = 3,
        });
        using var scope = Services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILogger<SpeakingEvaluationService>>();
        return new SpeakingEvaluationService(db, provider, options, logger);
    }

    public async Task RunEvaluationJobWithFakeProviderAsync(
        bool success = true,
        string? failureReason = null,
        double? overallScore = 78,
        double? fluencyScore = 72,
        string? transcript = "The learner spoke clearly.")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var provider = new FakeSpeakingEvaluationProvider(
            success: success, failureReason: failureReason,
            overallScore: overallScore, fluencyScore: fluencyScore, transcript: transcript);
        await BuildSvc(db, provider).ProcessPendingAsync(20);
    }

    public async Task RunEvaluationJobWithNoOpAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var provider = new NoOpSpeakingEvaluationProvider();
        await BuildSvc(db, provider, enabled: false).ProcessPendingAsync(20);
    }

    public async Task RunEvaluationJobWithThrowingProviderAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var provider = new FakeSpeakingEvaluationProvider(throws: true);
        await BuildSvc(db, provider).ProcessPendingAsync(20);
    }
}
