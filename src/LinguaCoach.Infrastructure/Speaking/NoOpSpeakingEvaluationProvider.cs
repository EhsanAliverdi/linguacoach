using LinguaCoach.Application.Speaking;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Safe no-op speaking evaluation provider.
/// Returns IsSupported=false so the job immediately resolves evaluations as NotSupported.
/// Used as the default when SpeakingEvaluation:Enabled=false or no real provider is configured.
/// No AI calls, no network I/O, no cost.
/// </summary>
public sealed class NoOpSpeakingEvaluationProvider : ISpeakingEvaluationProvider
{
    public string ProviderName => "NoOp";
    public bool IsSupported => false;
    public SpeakingEvaluationProviderCapabilities Capabilities => SpeakingEvaluationProviderCapabilities.None;

    public Task<SpeakingEvaluationProviderResult> EvaluateAsync(
        SpeakingEvaluationRequest request,
        CancellationToken ct = default) =>
        Task.FromResult(new SpeakingEvaluationProviderResult(
            Success: false,
            Transcript: null,
            OverallScore: null,
            FluencyScore: null,
            PronunciationScore: null,
            CompletenessScore: null,
            RelevanceScore: null,
            FeedbackText: null,
            SuggestedImprovement: null,
            FailureReason: "Speaking evaluation is not yet available.",
            ModelName: null));
}
