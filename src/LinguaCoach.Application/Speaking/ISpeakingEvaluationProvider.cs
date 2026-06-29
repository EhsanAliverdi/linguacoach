namespace LinguaCoach.Application.Speaking;

/// <summary>
/// Narrow interface for audio/speaking evaluation providers.
/// Separate from IAiProvider — audio evaluation requires audio input, not a rendered text prompt.
/// </summary>
public interface ISpeakingEvaluationProvider
{
    string ProviderName { get; }

    /// <summary>False when provider is disabled or not configured. Job uses NoOp fallback.</summary>
    bool IsSupported { get; }

    Task<SpeakingEvaluationProviderResult> EvaluateAsync(
        SpeakingEvaluationRequest request,
        CancellationToken ct = default);
}

public sealed record SpeakingEvaluationRequest(
    Guid AttemptId,
    Guid StudentProfileId,
    Guid ActivityId,
    string? AudioStorageKey,
    string? ActivityPrompt,
    string? ActivityTitle,
    string? CefrLevel,
    string CorrelationId);

public sealed record SpeakingEvaluationProviderResult(
    bool Success,
    string? Transcript,
    double? OverallScore,
    double? FluencyScore,
    double? PronunciationScore,
    double? CompletenessScore,
    double? RelevanceScore,
    string? FeedbackText,
    string? SuggestedImprovement,
    string? FailureReason,
    string? ModelName,
    int InputTokens = 0,
    int OutputTokens = 0,
    decimal CostUsd = 0m);
