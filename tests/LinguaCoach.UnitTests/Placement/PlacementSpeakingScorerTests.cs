using System.Text.Json;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Infrastructure.Placement;
using LinguaCoach.UnitTests.Speaking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Placement;

/// <summary>
/// Unit tests for PlacementSpeakingScorer — the bridge between a placement item's "speaking"
/// scoring rule and ISpeakingEvaluationProvider (mirrors PlacementScoringServiceTests' conventions
/// for the deterministic scorer).
/// </summary>
public sealed class PlacementSpeakingScorerTests
{
    private static ScoringRulesDocument SpeakingDoc(double points = 1.0) => new(
        new Dictionary<string, ComponentScoringRule>
        {
            ["answer"] = new(ScoringRuleKinds.Speaking, Points: points, RequiresManualOrAiEvaluation: true, Skill: "speaking"),
        });

    private static IReadOnlyDictionary<string, JsonElement> Submission(object data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    private static PlacementSpeakingScorer MakeSut(ISpeakingEvaluationProvider provider, double threshold = 0.6) =>
        new(provider, Options.Create(new PlacementAssessmentOptions { SpeakingPassThreshold = threshold }),
            NullLogger<PlacementSpeakingScorer>.Instance);

    [Fact]
    public void CanScore_ReturnsTrue_WhenAnyComponentIsSpeakingKind()
    {
        var sut = MakeSut(new FakeSpeakingEvaluationProvider());
        Assert.True(sut.CanScore(SpeakingDoc()));
    }

    [Fact]
    public void CanScore_ReturnsFalse_ForDeterministicKinds()
    {
        var doc = new ScoringRulesDocument(new Dictionary<string, ComponentScoringRule>
        {
            ["answer"] = new(ScoringRuleKinds.SingleChoice, CorrectAnswer: "A"),
        });
        var sut = MakeSut(new FakeSpeakingEvaluationProvider());
        Assert.False(sut.CanScore(doc));
    }

    [Fact]
    public async Task ScoreAsync_AboveThreshold_IsCorrectAndScoreNormalizedTo0To1()
    {
        var provider = new FakeSpeakingEvaluationProvider(success: true, overallScore: 80);
        var sut = MakeSut(provider, threshold: 0.6);

        var result = await sut.ScoreAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Describe your morning routine.", "A2",
            SpeakingDoc(), Submission(new { answer = new { storageKey = "placement-speaking/x.webm" } }));

        Assert.True(result.IsCorrect);
        Assert.Equal(0.8, result.Score, precision: 5);
        Assert.Single(result.Components);
        Assert.Equal("answer", result.Components[0].ComponentKey);
    }

    [Fact]
    public async Task ScoreAsync_BelowThreshold_IsNotCorrect()
    {
        var provider = new FakeSpeakingEvaluationProvider(success: true, overallScore: 30);
        var sut = MakeSut(provider, threshold: 0.6);

        var result = await sut.ScoreAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Describe your morning routine.", "A2",
            SpeakingDoc(), Submission(new { answer = new { storageKey = "placement-speaking/x.webm" } }));

        Assert.False(result.IsCorrect);
        Assert.Equal(0.0, result.Components[0].PointsEarned);
    }

    [Fact]
    public async Task ScoreAsync_NoStorageKeySubmitted_FailsGracefully()
    {
        var sut = MakeSut(new FakeSpeakingEvaluationProvider());

        var result = await sut.ScoreAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Prompt", "A2", SpeakingDoc(), Submission(new { answer = (object?)null }));

        Assert.False(result.IsCorrect);
        Assert.Equal(0.0, result.Score);
    }

    [Fact]
    public async Task ScoreAsync_ProviderNotSupported_FailsGracefully()
    {
        var sut = MakeSut(new UnsupportedSpeakingEvaluationProvider());

        var result = await sut.ScoreAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Prompt", "A2", SpeakingDoc(),
            Submission(new { answer = new { storageKey = "placement-speaking/x.webm" } }));

        Assert.False(result.IsCorrect);
        Assert.Contains("unavailable", result.EvaluationNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScoreAsync_ProviderThrows_FailsGracefully()
    {
        var sut = MakeSut(new FakeSpeakingEvaluationProvider(throws: true));

        var result = await sut.ScoreAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Prompt", "A2", SpeakingDoc(),
            Submission(new { answer = new { storageKey = "placement-speaking/x.webm" } }));

        Assert.False(result.IsCorrect);
    }

    private sealed class UnsupportedSpeakingEvaluationProvider : ISpeakingEvaluationProvider
    {
        public string ProviderName => "unsupported";
        public bool IsSupported => false;
        public SpeakingEvaluationProviderCapabilities Capabilities => SpeakingEvaluationProviderCapabilities.None;

        public Task<SpeakingEvaluationProviderResult> EvaluateAsync(
            SpeakingEvaluationRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("Should not be called when IsSupported is false.");
    }
}
