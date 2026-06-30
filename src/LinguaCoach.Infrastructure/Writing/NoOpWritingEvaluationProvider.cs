using LinguaCoach.Application.Writing;

namespace LinguaCoach.Infrastructure.Writing;

/// <summary>
/// Safe no-op writing evaluation provider.
/// Returns IsSupported=false so the job immediately resolves evaluations as NotSupported.
/// Used as the default when WritingEvaluation:Enabled=false or no real provider is configured.
/// No AI calls, no network I/O, no cost.
/// </summary>
public sealed class NoOpWritingEvaluationProvider : IWritingEvaluationProvider
{
    public string ProviderName => "NoOp";
    public bool IsSupported => false;
    public WritingEvaluationProviderCapabilities Capabilities => WritingEvaluationProviderCapabilities.None;

    public Task<WritingEvaluationProviderResult> EvaluateAsync(
        WritingEvaluationRequest request,
        CancellationToken ct = default) =>
        Task.FromResult(new WritingEvaluationProviderResult(
            Success: false,
            OverallScore: null,
            GrammarScore: null,
            VocabularyScore: null,
            CoherenceScore: null,
            TaskCompletionScore: null,
            FeedbackText: null,
            SuggestedImprovement: null,
            CorrectedText: null,
            FailureReason: "Writing evaluation is not yet available.",
            ModelName: null));
}
