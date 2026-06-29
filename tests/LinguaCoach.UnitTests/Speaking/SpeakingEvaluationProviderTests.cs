using FluentAssertions;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Infrastructure.Speaking;

namespace LinguaCoach.UnitTests.Speaking;

/// <summary>
/// Unit tests for speaking evaluation provider capability model and parsing logic.
/// Phase 16G — provider-backed evaluation controlled rollout.
/// </summary>
public sealed class SpeakingEvaluationProviderTests
{
    // ── NoOp provider ─────────────────────────────────────────────────────────

    [Fact]
    public void NoOpProvider_IsSupported_IsFalse()
    {
        var provider = new NoOpSpeakingEvaluationProvider();
        provider.IsSupported.Should().BeFalse();
    }

    [Fact]
    public void NoOpProvider_ProviderName_IsNoOp()
    {
        var provider = new NoOpSpeakingEvaluationProvider();
        provider.ProviderName.Should().Be("NoOp");
    }

    [Fact]
    public void NoOpProvider_Capabilities_AreAllFalse()
    {
        var provider = new NoOpSpeakingEvaluationProvider();
        var caps = provider.Capabilities;

        caps.SupportsAudioInput.Should().BeFalse();
        caps.SupportsTranscript.Should().BeFalse();
        caps.SupportsFluencyScore.Should().BeFalse();
        caps.SupportsPronunciationScore.Should().BeFalse();
        caps.SupportsStructuredOutput.Should().BeFalse();
        caps.SupportsRubricScoring.Should().BeFalse();
    }

    [Fact]
    public async Task NoOpProvider_EvaluateAsync_ReturnsNotSupportedResult()
    {
        var provider = new NoOpSpeakingEvaluationProvider();
        var request = MakeRequest();

        var result = await provider.EvaluateAsync(request);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
        result.OverallScore.Should().BeNull();
        result.FluencyScore.Should().BeNull();
        result.PronunciationScore.Should().BeNull();
        result.Transcript.Should().BeNull();
    }

    // ── Provider capability model ──────────────────────────────────────────────

    [Fact]
    public void CapabilitiesNone_AllFalse()
    {
        var caps = SpeakingEvaluationProviderCapabilities.None;

        caps.SupportsAudioInput.Should().BeFalse();
        caps.SupportsTranscript.Should().BeFalse();
        caps.SupportsFluencyScore.Should().BeFalse();
        caps.SupportsPronunciationScore.Should().BeFalse();
        caps.SupportsStructuredOutput.Should().BeFalse();
        caps.SupportsRubricScoring.Should().BeFalse();
    }

    [Fact]
    public void CapabilitiesOpenAiWhisperGpt_AudioAndTranscriptSupported_PronunciationNot()
    {
        var caps = SpeakingEvaluationProviderCapabilities.OpenAiWhisperGpt;

        caps.SupportsAudioInput.Should().BeTrue();
        caps.SupportsTranscript.Should().BeTrue();
        caps.SupportsFluencyScore.Should().BeTrue();
        caps.SupportsPronunciationScore.Should().BeFalse();
        caps.SupportsStructuredOutput.Should().BeTrue();
        caps.SupportsRubricScoring.Should().BeTrue();
    }

    // ── SpeakingEvaluationOptions defaults ────────────────────────────────────

    [Fact]
    public void Options_DefaultEnabled_IsFalse()
    {
        var opts = new SpeakingEvaluationOptions();
        opts.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Options_DefaultProvider_IsNoOp()
    {
        var opts = new SpeakingEvaluationOptions();
        opts.Provider.Should().Be("NoOp");
    }

    [Fact]
    public void Options_DefaultModel_IsGpt4oMini()
    {
        var opts = new SpeakingEvaluationOptions();
        opts.Model.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void Options_DefaultTranscriptionModel_IsWhisper1()
    {
        var opts = new SpeakingEvaluationOptions();
        opts.TranscriptionModel.Should().Be("whisper-1");
    }

    [Fact]
    public void Options_MaxAudioSizeBytes_Is25Mb()
    {
        var opts = new SpeakingEvaluationOptions();
        opts.MaxAudioSizeBytes.Should().Be(25 * 1024 * 1024);
    }

    // ── Fake provider for logic tests ─────────────────────────────────────────

    [Fact]
    public async Task FakeProvider_Success_ReturnsCompletedResult()
    {
        var provider = new FakeSpeakingEvaluationProvider(success: true);
        var result = await provider.EvaluateAsync(MakeRequest());

        result.Success.Should().BeTrue();
        result.FeedbackText.Should().NotBeNullOrWhiteSpace();
        result.SuggestedImprovement.Should().NotBeNullOrWhiteSpace();
        result.OverallScore.Should().BeInRange(0, 100);
        result.FailureReason.Should().BeNull();
        result.PronunciationScore.Should().BeNull("provider does not claim pronunciation scoring");
    }

    [Fact]
    public async Task FakeProvider_Failure_ReturnsFailedResult()
    {
        var provider = new FakeSpeakingEvaluationProvider(success: false, failureReason: "Test failure");
        var result = await provider.EvaluateAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Test failure");
        result.OverallScore.Should().BeNull();
        result.FeedbackText.Should().BeNull();
    }

    [Fact]
    public async Task FakeProvider_PartialResult_NullableFieldsAccepted()
    {
        var provider = new FakeSpeakingEvaluationProvider(
            success: true,
            overallScore: 70,
            fluencyScore: null,
            transcript: null);

        var result = await provider.EvaluateAsync(MakeRequest());

        result.Success.Should().BeTrue();
        result.OverallScore.Should().Be(70);
        result.FluencyScore.Should().BeNull();
        result.Transcript.Should().BeNull();
    }

    [Fact]
    public async Task FakeProvider_NoScores_DoesNotReturnFakeData()
    {
        var provider = new FakeSpeakingEvaluationProvider(
            success: true,
            overallScore: null,
            fluencyScore: null,
            completenessScore: null,
            relevanceScore: null);

        var result = await provider.EvaluateAsync(MakeRequest());

        result.OverallScore.Should().BeNull();
        result.FluencyScore.Should().BeNull();
        result.CompletenessScore.Should().BeNull();
        result.RelevanceScore.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SpeakingEvaluationRequest MakeRequest() => new(
        AttemptId: Guid.NewGuid(),
        StudentProfileId: Guid.NewGuid(),
        ActivityId: Guid.NewGuid(),
        AudioStorageKey: "speaking-recordings/test.webm",
        ActivityPrompt: "Describe your typical workday.",
        ActivityTitle: "Workplace Speaking",
        CefrLevel: "B1",
        CorrelationId: Guid.NewGuid().ToString("N"));
}

// ── Fake provider for unit / integration tests ────────────────────────────────

/// <summary>
/// Configurable fake speaking evaluation provider for tests.
/// Does not make network calls. Allows verification of provider-backed flows.
/// </summary>
public sealed class FakeSpeakingEvaluationProvider : ISpeakingEvaluationProvider
{
    public string ProviderName => "fake";
    public bool IsSupported => true;
    public SpeakingEvaluationProviderCapabilities Capabilities => SpeakingEvaluationProviderCapabilities.OpenAiWhisperGpt;

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
        if (_throws)
            throw new InvalidOperationException("FakeSpeakingEvaluationProvider: simulated provider throw.");

        return Task.FromResult(new SpeakingEvaluationProviderResult(
            Success: _success,
            Transcript: _success ? _transcript : null,
            OverallScore: _success ? _overallScore : null,
            FluencyScore: _success ? _fluencyScore : null,
            PronunciationScore: null,
            CompletenessScore: _success ? _completenessScore : null,
            RelevanceScore: _success ? _relevanceScore : null,
            FeedbackText: _success ? "This feedback is AI-assisted and may be approximate. Good effort on your response." : null,
            SuggestedImprovement: _success ? "Try to add a specific example next time." : null,
            FailureReason: _success ? null : (_failureReason ?? "Fake provider failure."),
            ModelName: _success ? "fake-model" : null));
    }
}
