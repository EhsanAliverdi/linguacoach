namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Immutable snapshot of score thresholds used by SpeakingDryRunSignalMapper.
/// Build from SpeakingEvaluationOptions; pass explicitly so tests can override without config.
/// </summary>
public sealed record SpeakingSignalThresholds(
    double MinPositiveOverall,
    double MinPositiveRelevance,
    double MinPositiveCompleteness,
    double MaxReviewOverall,
    double MaxReviewRelevance,
    double MaxReviewCompleteness)
{
    public static SpeakingSignalThresholds Default { get; } =
        new(80.0, 80.0, 80.0, 55.0, 55.0, 55.0);

    public static SpeakingSignalThresholds FromOptions(SpeakingEvaluationOptions opts) => new(
        opts.MinimumOverallScoreForPositiveSignal,
        opts.MinimumRelevanceScoreForPositiveSignal,
        opts.MinimumCompletenessScoreForPositiveSignal,
        opts.MaximumOverallScoreForReviewSignal,
        opts.MaximumRelevanceScoreForReviewSignal,
        opts.MaximumCompletenessScoreForReviewSignal);
}
